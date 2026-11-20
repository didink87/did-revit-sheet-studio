@echo off
chcp 65001 >nul
title Setup Sheet Studio (Revit 2026)
cd /d "%~dp0"
echo.
echo   SHEET STUDIO - Revit 2026
echo   -------------------------
echo   [1] Install / update
echo   [2] Uninstall
echo   [3] Exit
echo.
set /p pilih="Choose 1/2/3 then press Enter: "
if "%pilih%"=="1" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SheetStudio.ps1"
if "%pilih%"=="2" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SheetStudio.ps1" -Uninstall
echo.
pause
