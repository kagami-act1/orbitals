Unicode True
RequestExecutionLevel user
ManifestDPIAware true
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "nsDialogs.nsh"
Name "Orbitals"
OutFile "${SETUP_OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Orbitals"
InstallDirRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "InstallLocation"
BrandingText "Orbitals"
SetCompressor /SOLID lzma
VIProductVersion "${APP_VERSION}"
VIAddVersionKey /LANG=1041 "ProductName" "Orbitals"
VIAddVersionKey /LANG=1041 "FileDescription" "Orbitals セットアップ"
VIAddVersionKey /LANG=1041 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1041 "LegalCopyright" "Orbitals"
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_BGCOLOR "0F141B"
!define MUI_TEXTCOLOR "EDF3F6"
!define MUI_INSTFILESPAGE_COLORS "93E9C1 0F141B"
!define MUI_CUSTOMFUNCTION_GUIINIT ThemeWindow
!define MUI_CUSTOMFUNCTION_UNGUIINIT un.ThemeWindow
!define MUI_WELCOMEFINISHPAGE_BITMAP "${SIDEBAR}"
!define MUI_WELCOMEPAGE_TITLE "Orbitals セットアップ"
!define MUI_WELCOMEPAGE_TEXT "リングとマウスジェスチャーで、$\r$\nよく使う操作を実行するアプリです。$\r$\n$\r$\n保存先を選んでインストールできます。$\r$\n管理者権限は不要です。$\r$\n$\r$\n更新時も、現在の設定を引き継ぎます。"
!define MUI_PAGE_CUSTOMFUNCTION_SHOW ThemeWindow
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "Orbitalsを保存するフォルダーを選んでください。"
!define MUI_DIRECTORYPAGE_TEXT_DESTINATION "インストール先"
!define MUI_PAGE_CUSTOMFUNCTION_SHOW ThemeDirectory
!insertmacro MUI_PAGE_DIRECTORY
Page custom MigrationPage MigrationLeave
!define MUI_PAGE_CUSTOMFUNCTION_SHOW ThemeInstall
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\orbitals.exe"
!define MUI_FINISHPAGE_RUN_TEXT "設定画面を開く"
!define MUI_FINISHPAGE_TITLE "準備ができました"
!define MUI_FINISHPAGE_TEXT "Orbitalsをインストールしました。$\r$\n$\r$\nスタートメニューから設定画面を開けます。$\r$\n画面を閉じても、通知領域で動作を続けます。"
!ifdef INSTALL_TEST
  !define MUI_FINISHPAGE_RUN_NOTCHECKED
!endif
!define MUI_PAGE_CUSTOMFUNCTION_SHOW ThemeFinish
!insertmacro MUI_PAGE_FINISH
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.ThemeConfirm
!insertmacro MUI_UNPAGE_CONFIRM
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.ThemeInstall
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Japanese"
SetFont "Yu Gothic UI" 10
Var MigrationDirectory
Var MigrationInput
Var DataDirectory

!macro ThemeWindow Prefix
Function ${Prefix}ThemeWindow
  SetCtlColors $HWNDPARENT "EDF3F6" "0F141B"
  SetCtlColors $mui.Branding.Text "AFC1D0" "0F141B"
  SetCtlColors $mui.Branding.Background "AFC1D0" "0F141B"
  SetCtlColors $mui.Header.Text "EDF3F6" "0F141B"
  SetCtlColors $mui.Header.SubText "AFC1D0" "0F141B"
  ShowWindow $mui.Line.Standard ${SW_HIDE}
  ShowWindow $mui.Line.FullWindow ${SW_HIDE}
FunctionEnd
!macroend
!insertmacro ThemeWindow ""
!insertmacro ThemeWindow "un."

Function ThemeDirectory
  Call ThemeWindow
  System::Call 'uxtheme::SetWindowTheme(p $mui.DirectoryPage.DirectoryBox, w "", w "")'
  SetCtlColors $mui.DirectoryPage "EDF3F6" "0F141B"
  SetCtlColors $mui.DirectoryPage.Text "AFC1D0" "0F141B"
  SetCtlColors $mui.DirectoryPage.DirectoryBox "EDF3F6" "0F141B"
  SetCtlColors $mui.DirectoryPage.Directory "EDF3F6" "1B232D"
  SetCtlColors $mui.DirectoryPage.SpaceRequired "AFC1D0" "0F141B"
  SetCtlColors $mui.DirectoryPage.SpaceAvailable "AFC1D0" "0F141B"
FunctionEnd

Function ThemeFinish
  Call ThemeWindow
  System::Call 'uxtheme::SetWindowTheme(p $mui.FinishPage.Run, w "", w "")'
  SetCtlColors $mui.FinishPage.Run "EDF3F6" "0F141B"
FunctionEnd

!macro ThemeInstall Prefix
Function ${Prefix}ThemeInstall
  Call ${Prefix}ThemeWindow
  System::Call 'uxtheme::SetWindowTheme(p $mui.InstFilesPage.ProgressBar, w "", w "")'
  SendMessage $mui.InstFilesPage.ProgressBar ${PBM_SETBARCOLOR} 0 0xC1E993
  SendMessage $mui.InstFilesPage.ProgressBar ${PBM_SETBKCOLOR} 0 0x2D231B
  SetCtlColors $mui.InstFilesPage "EDF3F6" "0F141B"
  SetCtlColors $mui.InstFilesPage.Text "AFC1D0" "0F141B"
  SetCtlColors $mui.InstFilesPage.Log "EDF3F6" "1B232D"
FunctionEnd
!macroend
!insertmacro ThemeInstall ""
!insertmacro ThemeInstall "un."

Function un.ThemeConfirm
  Call un.ThemeWindow
  SetCtlColors $mui.UnConfirmPage "EDF3F6" "0F141B"
  SetCtlColors $mui.UnConfirmPage.Text "AFC1D0" "0F141B"
  SetCtlColors $mui.UnConfirmPage.DirectoryText "EDF3F6" "0F141B"
  SetCtlColors $mui.UnConfirmPage.Directory "EDF3F6" "1B232D"
FunctionEnd

!macro MigrateSetting Name
  ${If} ${FileExists} "$MigrationDirectory\${Name}"
  ${AndIfNot} ${FileExists} "$DataDirectory\${Name}"
    ClearErrors
    CopyFiles /SILENT "$MigrationDirectory\${Name}" "$DataDirectory"
    ${If} ${Errors}
      SetErrorLevel 5
      Abort "設定をコピーできませんでした。保存先のアクセス権を確認してください。"
    ${EndIf}
  ${EndIf}
!macroend

Function .onInit
  SetShellVarContext current
  SetRegView 64
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Orbitalsは64bit版のWindowsで動作します。"
    SetErrorLevel 1633
    Quit
  ${EndIf}
  ${GetParameters} $0
  ${GetOptions} $0 "/MIGRATE=" $MigrationDirectory
  StrCpy $DataDirectory "$LOCALAPPDATA\Orbitals"
!ifdef INSTALL_TEST
  StrCpy $DataDirectory "$INSTDIR\data"
!else
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\OrbitRing.Personal.v1") p.r0'
  ${If} $0 P<> 0
    System::Call 'kernel32::CloseHandle(p r0)'
    IfSilent +2
    MessageBox MB_ICONINFORMATION "Orbitalsが起動しています。通知領域のOrbitalsメニューから終了して、セットアップを実行し直してください。"
    SetErrorLevel 1618
    Quit
  ${EndIf}
!endif
FunctionEnd

Function MigrationPage
!ifdef INSTALL_TEST
  StrCpy $DataDirectory "$INSTDIR\data"
!endif
  ${If} ${FileExists} "$DataDirectory\settings.json"
    Abort
  ${EndIf}
  !insertmacro MUI_HEADER_TEXT "設定の引き継ぎ（任意）" "ポータブル版から移行する場合だけ指定します。"
  Call ThemeWindow
  nsDialogs::Create 1018
  Pop $0
  SetCtlColors $0 "EDF3F6" "0F141B"
  ${NSD_CreateLabel} 0 6u 100% 26u "以前のOrbitalsのフォルダーを選んでください。$\r$\n引き継がない場合は、空欄のまま進めます。"
  Pop $1
  SetCtlColors $1 "EDF3F6" "0F141B"
  ${NSD_CreateDirRequest} 0 42u 76% 16u "$MigrationDirectory"
  Pop $MigrationInput
  SetCtlColors $MigrationInput "EDF3F6" "1B232D"
  ${NSD_CreateBrowseButton} 79% 40u 21% 20u "フォルダー…"
  Pop $1
  ${NSD_OnClick} $1 BrowseMigration
  ${NSD_CreateLabel} 0 78u 100% 32u "設定と未保存の下書きをコピーします。$\r$\n元のファイルは変更しません。"
  Pop $1
  SetCtlColors $1 "B6C2CC" "0F141B"
  nsDialogs::Show
FunctionEnd

Function BrowseMigration
  nsDialogs::SelectFolderDialog "以前のOrbitalsのフォルダー" "$MigrationDirectory"
  Pop $0
  StrCmp $0 error +2
  ${NSD_SetText} $MigrationInput $0
FunctionEnd

Function MigrationLeave
  ${NSD_GetText} $MigrationInput $MigrationDirectory
  StrCmp $MigrationDirectory "" done
  IfFileExists "$MigrationDirectory\settings.json" done
  MessageBox MB_ICONEXCLAMATION "選んだフォルダーにsettings.jsonがありません。以前のOrbitalsのフォルダーを選んでください。"
  Abort
  done:
FunctionEnd

Section "Orbitals"
  SetShellVarContext current
  SetRegView 64
!ifdef INSTALL_TEST
  StrCpy $DataDirectory "$INSTDIR\data"
!endif
  StrCmp $MigrationDirectory "" migrationDone
  IfFileExists "$MigrationDirectory\settings.json" +3
    SetErrorLevel 2
    Abort "引き継ぐ設定ファイルが見つかりません。"
  IfFileExists "$DataDirectory\settings.json" migrationDone
  CreateDirectory "$DataDirectory"
  !insertmacro MigrateSetting "settings.json.bak"
  !insertmacro MigrateSetting "settings.json.draft.json"
  !insertmacro MigrateSetting "settings.json"
  migrationDone:
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /r "${PAYLOAD}\*.*"
  FileOpen $0 "$INSTDIR\installation.json" w
  FileWrite $0 '{$\"Mode$\":$\"installed$\"}'
  FileClose $0
  WriteUninstaller "$INSTDIR\Uninstall.exe"
!ifndef INSTALL_TEST
  CreateDirectory "$SMPROGRAMS\Orbitals"
  CreateShortCut "$SMPROGRAMS\Orbitals\Orbitals.lnk" "$INSTDIR\orbitals.exe" "" "$INSTDIR\orbitals.exe"
  CreateShortCut "$SMPROGRAMS\Orbitals\アンインストール.lnk" "$INSTDIR\Uninstall.exe"
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "DisplayName" "Orbitals"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "DisplayIcon" "$INSTDIR\orbitals.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "NoRepair" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals" "EstimatedSize" ${APP_SIZE}
!endif
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  IfFileExists "$INSTDIR\installation.json" +4
    MessageBox MB_ICONSTOP "Orbitalsのインストール先を確認できないため、削除を中止しました。"
    SetErrorLevel 2
    Quit
!ifndef INSTALL_TEST
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\OrbitRing.Personal.v1") p.r0'
  ${If} $0 P<> 0
    System::Call 'kernel32::CloseHandle(p r0)'
    IfSilent +2
    MessageBox MB_ICONINFORMATION "Orbitalsを通知領域のメニューから終了してから、アンインストールしてください。"
    SetErrorLevel 1618
    Quit
  ${EndIf}
!endif
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  SetRegView 64
  !include "${UNINSTALL_FILES}"
  Delete "$INSTDIR\installation.json"
  Delete "$INSTDIR\Uninstall.exe"
!ifndef INSTALL_TEST
  Delete "$SMPROGRAMS\Orbitals\Orbitals.lnk"
  Delete "$SMPROGRAMS\Orbitals\アンインストール.lnk"
  RMDir "$SMPROGRAMS\Orbitals"
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "orbitals"
  StrCmp $0 '$\"$INSTDIR\orbitals.exe$\" --background' 0 +2
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "orbitals"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbitals"
!endif
  ; Remove only files shipped by this installer. Keep settings and user files.
  RMDir "$INSTDIR"
SectionEnd
