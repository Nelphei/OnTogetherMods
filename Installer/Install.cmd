@echo off
rem Installs r2modman and every SoapieScripts mod for On-Together. Double-click to run.
rem Uses Install.ps1 from this folder, or the newest one from GitHub if it isn't here.
setlocal
set "SCRIPT=%~dp0Install.ps1"
set "URL=https://raw.githubusercontent.com/Nelphei/OnTogetherMods/master/Installer/Install.ps1"
if exist "%SCRIPT%" powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
if not exist "%SCRIPT%" powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; irm '%URL%' | iex"
echo.
pause
