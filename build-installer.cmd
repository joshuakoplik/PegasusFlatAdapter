@echo off
rem Builds the driver and the installer. Needs the .NET SDK and NSIS (makensis on PATH,
rem or installed in the default "Program Files (x86)\NSIS" folder).
setlocal
cd /d %~dp0
dotnet build src\PegasusFlatPanel.csproj -c Release || exit /b 1
set MAKENSIS=makensis
where makensis >nul 2>&1 || set MAKENSIS="%ProgramFiles(x86)%\NSIS\makensis.exe"
%MAKENSIS% installer\PegasusFlatPanel.nsi || exit /b 1
echo Installer: installer\PegasusFlatPanelSetup-1.0.0.exe
