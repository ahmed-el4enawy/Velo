; Velo NSIS Per-User Windows 10 x64 Installer Script
; Strict compliance with Velo PRD v2.0 Section 1A & FR-13

!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

Name "Velo Download Accelerator"
OutFile "..\..\artifacts\Velo-win10-x64-setup.exe"
InstallDir "$LOCALAPPDATA\Programs\Velo"
RequestExecutionLevel user ; Per-user installation, no admin elevation required

; Interface Configuration
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function .onInit
    ; Strict Windows 10 x64 OS Verification (PRD Section 1A)
    ${IfNot} ${RunningX64}
        MessageBox MB_ICONSTOP "Velo requires a 64-bit (x64) operating system. 32-bit and ARM architectures are not supported."
        Abort
    ${EndIf}

    ${IfNot} ${IsWin10}
        MessageBox MB_ICONSTOP "Velo is verified and supported exclusively on Windows 10 (version 22H2 64-bit)."
        Abort
    ${EndIf}
FunctionEnd

Section "Velo Core Application" SecCore
    SetOutPath "$INSTDIR"
    File /r "..\..\publish\win-x64\*.*"

    ; Create Start Menu Shortcuts
    CreateDirectory "$SMPROGRAMS\Velo"
    CreateShortcut "$SMPROGRAMS\Velo\Velo.lnk" "$INSTDIR\Velo.exe"
    CreateShortcut "$SMPROGRAMS\Velo\Uninstall Velo.lnk" "$INSTDIR\Uninstall.exe"

    ; Write Uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"

    ; Write Native Messaging Host Manifest
    FileOpen $0 "$INSTDIR\com.velo.downloader.json" w
    FileWrite $0 '{"name":"com.velo.downloader","description":"Velo High-Speed Download Acceleration Bridge","path":"$INSTDIR\\Velo.NativeMessaging.exe","type":"stdio","allowed_origins":["chrome-extension://akobbbjojgceldlhaeiidogehdomjinn/"]}'
    FileClose $0

    ; Register Native Messaging Host for Chrome and Edge
    WriteRegStr HKCU "Software\Google\Chrome\NativeMessagingHosts\com.velo.downloader" "" "$INSTDIR\com.velo.downloader.json"
    WriteRegStr HKCU "Software\Microsoft\Edge\NativeMessagingHosts\com.velo.downloader" "" "$INSTDIR\com.velo.downloader.json"

    ; Registry entries for Add/Remove Programs (HKCU)
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Velo" "DisplayName" "Velo Download Accelerator"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Velo" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Velo" "DisplayIcon" "$INSTDIR\Velo.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Velo" "Publisher" "Ahmed Elshenawy"
SectionEnd

Section "Uninstall"
    Delete "$SMPROGRAMS\Velo\Velo.lnk"
    Delete "$SMPROGRAMS\Velo\Uninstall Velo.lnk"
    RMDir "$SMPROGRAMS\Velo"

    ; Unregister Native Messaging Host
    DeleteRegKey HKCU "Software\Google\Chrome\NativeMessagingHosts\com.velo.downloader"
    DeleteRegKey HKCU "Software\Microsoft\Edge\NativeMessagingHosts\com.velo.downloader"

    RMDir /r "$INSTDIR"
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Velo"
SectionEnd
