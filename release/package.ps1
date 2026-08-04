# Assemble le paquet de release public d'Overkit (binaires uniquement).
#
#   .\release\package.ps1 -Version 0.2.0-alpha
#     → paquet allégé : le runtime .NET n'est pas embarqué (le joueur installe
#       le .NET 8 Desktop Runtime, lien dans le README). Le Windows App SDK
#       reste embarqué : un seul prérequis. Sans le runtime .NET dans le
#       paquet, plus de vcruntime140_cor3.dll — c'est ce chargement qui
#       déclenchait l'heuristique de sideloading des scanners (ADR-0006).
#
#   .\release\package.ps1 -Version 0.2.0-alpha -SelfContained
#     → paquet autonome, aucun prérequis, mais ~300 Mo et heuristique probable.
#
# Note : on empaquette la sortie de `dotnet build` et non de `dotnet publish` —
# le publish WinUI non packagé perd le fichier .pri et les vues compilées
# (.xbf), ce qui fait planter le panneau au démarrage.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$SelfContained,
    [string]$DatasetDir = "$PSScriptRoot\..\..\dataset-local\out",
    [string]$ProbeDll = "$PSScriptRoot\..\..\probe-workspace\build\OverkitProbe\Game__Shipping__Win64\main.dll"
)

$ErrorActionPreference = 'Stop'
# Deux paquets, un par composant — c'est ce que le joueur installe séparément :
#   Overkit-Overlay-<version>-win-x64.zip : l'application (GitHub)
#   Overkit-Probe-<version>.zip           : le mod UE4SS (GitHub + Nexus)
$repo = Resolve-Path "$PSScriptRoot\.."
$stage = "$PSScriptRoot\out\Overkit-Overlay-$Version"
$zip = "$PSScriptRoot\out\Overkit-Overlay-$Version-win-x64.zip"
$build = "$PSScriptRoot\out\build-$Version"

Write-Host "Compilation du host ($(if ($SelfContained) { 'autonome' } else { 'runtime .NET requis' }))..."
if (Test-Path $build) { Remove-Item $build -Recurse -Force }
dotnet build "$repo\host\Overkit.Host" -c Release -r win-x64 `
    --self-contained $(if ($SelfContained) { 'true' } else { 'false' }) `
    -p:WindowsAppSDKSelfContained=true -o $build --nologo -v q | Out-Null

if (-not (Test-Path "$build\Overkit.Host.pri")) {
    throw "Overkit.Host.pri manquant : le panneau planterait au demarrage."
}

Write-Host "Compilation des modules..."
Get-ChildItem "$repo\modules" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    dotnet build $_.FullName -c Release --nologo -v q | Out-Null
}

Write-Host "Assemblage du paquet overlay..."
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\Overkit\data" | Out-Null
New-Item -ItemType Directory -Force "$stage\Overkit\Cards" | Out-Null

# Binaires du host (hors artefacts de compilation)
Get-ChildItem $build -Recurse -File |
    Where-Object { $_.Extension -notin '.pdb', '.xml' } |
    ForEach-Object {
        $target = Join-Path "$stage\Overkit" $_.FullName.Substring($build.Length).TrimStart('\')
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item $_.FullName $target
    }

Copy-Item "$DatasetDir\*.json" "$stage\Overkit\data\"
Copy-Item "$repo\dataset\map_calibration.draft.json" "$stage\Overkit\data\map_calibration.json"
Copy-Item "$repo\cards\*.json" "$stage\Overkit\Cards\" -ErrorAction SilentlyContinue

# Modules fournis
Get-ChildItem "$repo\modules" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $dll = Get-ChildItem "$($_.FullName)\bin\Release\net8.0" -Filter 'Overkit.Module.*.dll' -ErrorAction SilentlyContinue |
           Select-Object -First 1
    if ($dll) {
        $target = "$stage\Overkit\Modules\$($_.Name)"
        New-Item -ItemType Directory -Force $target | Out-Null
        Copy-Item $dll.FullName $target
    }
}

Copy-Item "$repo\release\LICENSE-BINARY.txt" "$stage\LICENSE.txt"

Compress-Archive -Path $stage -DestinationPath $zip -Force
Remove-Item $build -Recurse -Force
Write-Host ("OK : {0} ({1:N1} Mo)" -f $zip, ((Get-Item $zip).Length / 1MB))

# Second paquet : la sonde seule. Nexus Mods héberge le mod de jeu, tandis que
# l'application compagnon reste sur GitHub — un exécutable .NET non signé
# déclenche des heuristiques génériques qui bloquent la modération (ADR-0006).
$probeStage = "$PSScriptRoot\out\Overkit-Probe-$Version"
$probeZip = "$PSScriptRoot\out\Overkit-Probe-$Version.zip"
if (Test-Path $probeStage) { Remove-Item $probeStage -Recurse -Force }
New-Item -ItemType Directory -Force "$probeStage\OverkitProbe\dlls" | Out-Null
Copy-Item $ProbeDll "$probeStage\OverkitProbe\dlls\main.dll"
Copy-Item "$repo\probe\mapping.json" "$probeStage\OverkitProbe\mapping.json"
Set-Content "$probeStage\OverkitProbe\enabled.txt" '' -Encoding ascii
Copy-Item "$repo\release\LICENSE-BINARY.txt" "$probeStage\LICENSE.txt"
Compress-Archive -Path "$probeStage\*" -DestinationPath $probeZip -Force
Write-Host ("OK : {0} ({1:N0} Ko)" -f $probeZip, ((Get-Item $probeZip).Length / 1KB))
