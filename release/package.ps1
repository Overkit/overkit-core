# Assemble le paquet de release public d'Overkit (binaires uniquement).
# Usage : .\release\package.ps1 -Version 0.1.0-alpha
# Produit : release\out\Overkit-<version>-win-x64.zip
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$DatasetDir = "$PSScriptRoot\..\..\dataset-local\out",
    [string]$ProbeDll = "$PSScriptRoot\..\..\probe-workspace\build\OverkitProbe\Game__Shipping__Win64\main.dll"
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot\.."
$stage = "$PSScriptRoot\out\Overkit-$Version"
$zip = "$PSScriptRoot\out\Overkit-$Version-win-x64.zip"

Write-Host "Publication du host (self-contained win-x64)..."
dotnet publish "$repo\host\Overkit.Host" -c Release -r win-x64 --self-contained true | Out-Null
$publish = "$repo\host\Overkit.Host\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

Write-Host "Assemblage du paquet..."
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\Overkit\data" | Out-Null
New-Item -ItemType Directory -Force "$stage\PalworldMod\OverkitProbe\dlls" | Out-Null

Copy-Item "$publish\*" "$stage\Overkit\" -Recurse
Copy-Item "$DatasetDir\*.json" "$stage\Overkit\data\"
Copy-Item "$repo\dataset\map_calibration.draft.json" "$stage\Overkit\data\map_calibration.json"
Copy-Item $ProbeDll "$stage\PalworldMod\OverkitProbe\dlls\main.dll"
Copy-Item "$repo\probe\mapping.json" "$stage\PalworldMod\OverkitProbe\mapping.json"
Set-Content "$stage\PalworldMod\OverkitProbe\enabled.txt" '' -Encoding ascii
Copy-Item "$repo\release\LICENSE-BINARY.txt" "$stage\LICENSE.txt"

Compress-Archive -Path $stage -DestinationPath $zip -Force
Write-Host ("OK : {0} ({1:N1} Mo)" -f $zip, ((Get-Item $zip).Length / 1MB))
