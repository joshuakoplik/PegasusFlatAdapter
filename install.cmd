@echo off
rem Registers the Pegasus PWM Flat Panel driver for 32-bit and 64-bit ASCOM clients.
rem Run from an elevated (Administrator) command prompt, from the folder that holds
rem ASCOM.PegasusFlatPanel.CoverCalibrator.dll and ASCOM.*.dll.
setlocal
set DLL=%~dp0ASCOM.PegasusFlatPanel.CoverCalibrator.dll
if not exist "%DLL%" set DLL=%~dp0src\bin\Release\net48\ASCOM.PegasusFlatPanel.CoverCalibrator.dll
if not exist "%DLL%" (echo Cannot find ASCOM.PegasusFlatPanel.CoverCalibrator.dll & exit /b 1)

"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /codebase /nologo "%DLL%" || exit /b 1
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" (
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase /nologo "%DLL%" || exit /b 1
)
echo.
echo Registered. Choose "Pegasus PWM Flat Panel" in the ASCOM Chooser (Cover Calibrator) and open Properties to set it up.
