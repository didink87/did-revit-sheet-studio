# Builds the release package: dist\SheetStudio-<version>-Revit2026.zip
# Usage:  .\setup\Build-Release.ps1 -Version 1.1.0
param([string]$Version = '1.1.0')
$ErrorActionPreference = 'Stop'
$root  = Split-Path $PSScriptRoot -Parent
$name  = "SheetStudio-$Version-Revit2026"
$stage = Join-Path $root "dist\$name"
$zip   = Join-Path $root "dist\$name.zip"

# Build into its own folder so it does not clash with DLLs Revit has loaded.
$out = Join-Path $root 'dist\build'
dotnet build (Join-Path $root 'src\SheetStudio.Revit') -c Release -o $out
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip)   { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'bin') | Out-Null

Copy-Item (Join-Path $out 'SheetStudio.*.dll'), (Join-Path $out 'SheetStudio.Revit.deps.json') (Join-Path $stage 'bin')
Copy-Item (Join-Path $PSScriptRoot 'Setup.bat'), (Join-Path $PSScriptRoot 'Install-SheetStudio.ps1') $stage
Copy-Item (Join-Path $root 'docs\PANDUAN.md') (Join-Path $stage 'PANDUAN.md')
Copy-Item (Join-Path $PSScriptRoot 'BACA-DULU.txt') $stage

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "Package: $zip" -ForegroundColor Green
