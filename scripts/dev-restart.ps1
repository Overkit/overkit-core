# Cycle de développement Overkit : arrête le host, recompile, redéploie les
# Cards et les modules, relance, et affiche ce qui a été chargé.
#
#   .\scripts\dev-restart.ps1            build complet (host + modules)
#   .\scripts\dev-restart.ps1 -Fast      pas de build — pour une simple Card modifiée
#   .\scripts\dev-restart.ps1 -NoLaunch  prépare tout sans relancer l'overlay
param(
    [switch]$Fast,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot\.."
$hostDir = "$repo\host\Overkit.Host\bin\Release\net8.0-windows10.0.19041.0\win-x64"
$exe = "$hostDir\Overkit.Host.exe"

# 1) Arrêt du host (il verrouille ses DLL tant qu'il tourne)
$running = Get-Process Overkit.Host -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[1/4] Arret du host..." -ForegroundColor Cyan
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 400
} else {
    Write-Host "[1/4] Host deja arrete." -ForegroundColor DarkGray
}

# 2) Compilation
if ($Fast) {
    Write-Host "[2/4] Build ignore (-Fast)." -ForegroundColor DarkGray
} else {
    Write-Host "[2/4] Compilation du host et des modules..." -ForegroundColor Cyan
    dotnet build "$repo\host\Overkit.Host" -c Release --nologo -v q | Out-Null
    Get-ChildItem "$repo\modules" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        dotnet build $_.FullName -c Release --nologo -v q | Out-Null
    }
}

# 3) Déploiement des Cards fournies et des modules à côté de l'exécutable.
# Modules purgés (un module déplacé laisserait un doublon fantôme) ; Cards
# fournies écrasées sans purge. Les cards créées in-game vivent dans
# %LOCALAPPDATA%\Overkit\Cards et ne sont jamais touchées ici.
Write-Host "[3/4] Deploiement des cards fournies et des modules..." -ForegroundColor Cyan
if (Test-Path "$hostDir\Modules") { Remove-Item "$hostDir\Modules" -Recurse -Force }
New-Item -ItemType Directory -Force "$hostDir\Modules" | Out-Null
New-Item -ItemType Directory -Force "$hostDir\Cards" | Out-Null

$cards = Get-ChildItem "$repo\cards" -Filter *.json -ErrorAction SilentlyContinue
foreach ($card in $cards) {
    Copy-Item $card.FullName "$hostDir\Cards\" -Force
}

$moduleCount = 0
Get-ChildItem "$repo\modules" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $dll = Get-ChildItem "$($_.FullName)\bin\Release\net8.0" -Filter 'Overkit.Module.*.dll' -ErrorAction SilentlyContinue |
           Select-Object -First 1
    if ($dll) {
        $target = "$hostDir\Modules\$($_.Name)"
        New-Item -ItemType Directory -Force $target | Out-Null
        Copy-Item $dll.FullName $target -Force
        $moduleCount++
    }
}
Write-Host "      $($cards.Count) card(s), $moduleCount module(s)" -ForegroundColor DarkGray

# 4) Relance et compte rendu
if ($NoLaunch) {
    Write-Host "[4/4] Relance ignoree (-NoLaunch)." -ForegroundColor DarkGray
    return
}

Write-Host "[4/4] Relance de l'overlay..." -ForegroundColor Cyan
$log = "$hostDir\overkit.log"
Remove-Item $log -ErrorAction SilentlyContinue
Start-Process $exe
Start-Sleep -Seconds 4

if (Test-Path $log) {
    Write-Host ""
    Write-Host "--- Chargement ---" -ForegroundColor Green
    Get-Content $log -Encoding utf8 |
        Select-String -Pattern 'Module|Card|Dataset|Sonde|illisible|inactif|ignor' |
        ForEach-Object { Write-Host "  $($_.Line)" }
    Write-Host ""
    Write-Host "Pret. F6 en jeu pour ouvrir le panneau." -ForegroundColor Green
}
