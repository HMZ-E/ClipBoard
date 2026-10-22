@echo off
cd /d "%~dp0"
if not exist "build\ClipBoard.exe" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if not exist "build\ClipBoard.exe" exit /b 1
start "" "%~dp0build\ClipBoard.exe"
