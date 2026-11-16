# Sheet Studio - pemasang sederhana untuk Revit 2026
# Pakai:  .\Install-SheetStudio.ps1            (pasang / perbarui)
#         .\Install-SheetStudio.ps1 -Uninstall (copot)
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
Write-Host '=== Sheet Studio untuk Revit' $RevitVersion '===' -ForegroundColor Cyan

if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
    Write-Host 'Revit sedang terbuka. Tutup semua jendela Revit dulu, lalu jalankan lagi.' -ForegroundColor Yellow
    exit 2
}

if ($Uninstall) {
    if (Test-Path $manifest)  { Remove-Item $manifest -Force }
    if (Test-Path $targetDir) { Remove-Item $targetDir -Recurse -Force }
    Write-Host 'Sheet Studio sudah dicopot.' -ForegroundColor Green
    Write-Host "(Preferensi & judul tersimpan tetap ada di $env:APPDATA\SheetStudio)"
    exit 0
}

$dll = Join-Path $Source 'SheetStudio.Revit.dll'
if (-not (Test-Path $dll)) {
    Write-Host "File add-in tidak ditemukan: $dll" -ForegroundColor Red
    Write-Host 'Pastikan folder "bin" ada di sebelah skrip ini (ekstrak seluruh isi ZIP).'
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

Write-Host 'Terpasang:' -ForegroundColor Green
Write-Host "  $manifest"
Write-Host "  $targetDir"
Write-Host ''
Write-Host 'Buka Revit 2026 -> tab DID -> Sheet Studio.'
Write-Host 'Bila muncul dialog keamanan add-in, pilih "Always Load".'
