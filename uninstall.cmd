@echo off
rem Unregisters the Pegasus PWM Flat Panel driver. Run as Administrator.
setlocal
set DLL=%~dp0ASCOM.PegasusFlatPanel.CoverCalibrator.dll
if not exist "%DLL%" set DLL=%~dp0src\bin\Release\net48\ASCOM.PegasusFlatPanel.CoverCalibrator.dll
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /unregister /nologo "%DLL%"
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" (
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /unregister /nologo "%DLL%"
)
