# Sheet Studio - simple installer for Revit 2026
# Usage:  .\Install-SheetStudio.ps1            (install / update)
#         .\Install-SheetStudio.ps1 -Uninstall (uninstall)
param(
    [switch]$Uninstall,
    [string]$Source = (Join-Path $PSScriptRoot 'bin'),
    [string]$RevitVersion = '2026'
)
$ErrorActionPreference = 'Stop'
$addinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$targetDir = Join-Path $addinsDir 'SheetStudio'
$manifest  = Join-Path $addinsDir 'SheetStudio.addin'

Write-Host ''
Write-Host '=== Sheet Studio for Revit' $RevitVersion '===' -ForegroundColor Cyan

if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
    Write-Host 'Revit is running. Close all Revit windows first, then run this again.' -ForegroundColor Yellow
    exit 2
}

if ($Uninstall) {
    if (Test-Path $manifest)  { Remove-Item $manifest -Force }
    if (Test-Path $targetDir) { Remove-Item $targetDir -Recurse -Force }
    Write-Host 'Sheet Studio has been uninstalled.' -ForegroundColor Green
    Write-Host "(Preferences & saved titles are kept in $env:APPDATA\SheetStudio)"
    exit 0
}

$dll = Join-Path $Source 'SheetStudio.Revit.dll'
if (-not (Test-Path $dll)) {
    Write-Host "Add-in file not found: $dll" -ForegroundColor Red
    Write-Host 'Make sure the "bin" folder sits next to this script (extract the whole ZIP).'
    exit 1
}

New-Item -ItemType Directory -Force $targetDir | Out-Null
Copy-Item (Join-Path $Source '*') $targetDir -Recurse -Force
Get-ChildItem $targetDir -Recurse -File | Unblock-File

$assembly = Join-Path $targetDir 'SheetStudio.Revit.dll'
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Sheet Studio</Name>
    <Assembly>$assembly</Assembly>
    <AddInId>E4A9D2B7-6C31-4F85-9B0E-2D7C5A8F1E63</AddInId>
    <FullClassName>SheetStudio.Revit.App</FullClassName>
    <VendorId>DIDF</VendorId>
    <VendorDescription>Sheet Studio</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
[System.IO.File]::WriteAllText($manifest, $xml, (New-Object System.Text.UTF8Encoding($false)))

Write-Host 'Installed:' -ForegroundColor Green
Write-Host "  $manifest"
Write-Host "  $targetDir"
Write-Host ''
Write-Host 'Open Revit 2026 -> DID tab -> Sheet Studio.'
Write-Host 'If an add-in security dialog appears, choose "Always Load".'
