; NSIS installer for the Pegasus PWM Flat Panel ASCOM driver.
; Build:  makensis installer/PegasusFlatPanel.nsi      (works on Windows or Linux)
; Expects the Release build in ../src/bin/Release/net48 (run dotnet build first).

Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

!define APPNAME     "Pegasus PWM Flat Panel"
!define DRIVERID    "ASCOM.PegasusFlatPanel.CoverCalibrator"
!ifndef VERSION
  !define VERSION   "1.0.0"
!endif
!define DLLNAME     "${DRIVERID}.dll"
!define BUILDDIR    "..\src\bin\Release\net48"
!define UNINSTKEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\PegasusFlatPanel"
!define REGASM32    "$WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"
!define REGASM64    "$WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"

Name "${APPNAME} ${VERSION}"
OutFile "PegasusFlatPanelSetup-${VERSION}.exe"
InstallDir "$COMMONFILES32\ASCOM\CoverCalibrator\PegasusFlatPanel"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName"     "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} ASCOM driver installer"
VIAddVersionKey "FileVersion"     "${VERSION}"
VIAddVersionKey "ProductVersion"  "${VERSION}"
VIAddVersionKey "LegalCopyright"  ""

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "This installs the ${APPNAME} ASCOM CoverCalibrator driver.$\r$\n$\r$\nIt turns a PWM output (dew heater or adjustable port) on a Pegasus Astro power box into a flat panel, using the Pegasus ASCOM Switch driver.$\r$\n$\r$\nThe ASCOM Platform and the Pegasus Astro ASCOM drivers must already be installed."
!define MUI_FINISHPAGE_TEXT "Installed.$\r$\n$\r$\nIn your imaging application, open the ASCOM Chooser for Cover Calibrator / Flat Panel, select '${APPNAME}' and click Properties to choose the Pegasus driver and PWM output.$\r$\n$\r$\nRemember to turn Auto-Dew off for that port in the Pegasus software."
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Show README"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; .NET Framework 4.8 or later (Release >= 528040)
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_ICONSTOP|MB_OK ".NET Framework 4.8 or later is required.$\r$\nInstall it from Microsoft and run this installer again."
    Abort
  ${EndIf}

  ; ASCOM Platform (its Profile component is registered as a COM class)
  ReadRegStr $0 HKCR "ASCOM.Utilities.Profile\CLSID" ""
  ${If} $0 == ""
    MessageBox MB_ICONSTOP|MB_OK "The ASCOM Platform is not installed.$\r$\nInstall it from ascom-standards.org and run this installer again."
    Abort
  ${EndIf}
FunctionEnd

; Runs RegAsm on the driver DLL and aborts the install on failure.
!macro RunRegAsm EXE ARGS
  nsExec::ExecToLog '"${EXE}" ${ARGS} /nologo "$INSTDIR\${DLLNAME}"'
  Pop $0
  ${If} $0 != 0
    DetailPrint "RegAsm failed with exit code $0"
    MessageBox MB_ICONSTOP|MB_OK "Registering the driver failed (RegAsm exit code $0). See the details list."
    Abort
  ${EndIf}
!macroend

Section "Driver" SecDriver
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "${BUILDDIR}\${DLLNAME}"
  File "${BUILDDIR}\ASCOM.DeviceInterfaces.dll"
  File "${BUILDDIR}\ASCOM.Exceptions.dll"
  File "/oname=README.txt" "..\README.md"

  DetailPrint "Registering for 32-bit applications..."
  !insertmacro RunRegAsm "${REGASM32}" "/codebase"
  ${If} ${RunningX64}
    DetailPrint "Registering for 64-bit applications..."
    !insertmacro RunRegAsm "${REGASM64}" "/codebase"
  ${EndIf}

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr   HKLM "${UNINSTKEY}" "DisplayName"     "${APPNAME} (ASCOM driver)"
  WriteRegStr   HKLM "${UNINSTKEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKLM "${UNINSTKEY}" "Publisher"       "${APPNAME}"
  WriteRegStr   HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ${If} ${RunningX64}
    nsExec::ExecToLog '"${REGASM64}" /unregister /nologo "$INSTDIR\${DLLNAME}"'
    Pop $0
  ${EndIf}
  nsExec::ExecToLog '"${REGASM32}" /unregister /nologo "$INSTDIR\${DLLNAME}"'
  Pop $0

  Delete "$INSTDIR\${DLLNAME}"
  Delete "$INSTDIR\ASCOM.DeviceInterfaces.dll"
  Delete "$INSTDIR\ASCOM.Exceptions.dll"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTKEY}"
SectionEnd
