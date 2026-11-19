; HackPDM Windows installer (Inno Setup 6.3+), built by .github/workflows/windows-release.yml:
;   iscc /DAppVersion=1.2.0 /DSourceDir=..\publish\HackPDM /DOutputDir=..\artifacts installer\HackPDM.iss
; Installs for the current user only (no administrator rights), so the in-app updater can run it.
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\HackPDM"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
; keep this id: it is how an update finds the installed copy
AppId={{A996291B-F998-4B0D-BF43-24A28399F485}
AppName=HackPDM
AppVersion={#AppVersion}
AppVerName=HackPDM {#AppVersion}
AppPublisher=HackPDM
AppPublisherURL=https://github.com/moonxbears/wyrmpdm
AppSupportURL=https://github.com/moonxbears/wyrmpdm/issues
AppUpdatesURL=https://github.com/moonxbears/wyrmpdm/releases
DefaultDirName={localappdata}\Programs\HackPDM
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=HackPDM-{#AppVersion}-win-x64-setup
SetupIconFile=..\Src\HackPDM\Assets\hackpdm-icon.ico
LicenseFile=..\LICENSE
VersionInfoProductName=HackPDM
VersionInfoCompany=HackPDM
VersionInfoDescription=HackPDM Setup — Open-source PDM client for CAD & Odoo
VersionInfoVersion={#AppVersion}.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; an update closes the running app first (the updater exits it as well)
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\HackPDM.exe
UninstallDisplayName=HackPDM

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\HackPDM"; Filename: "{app}\HackPDM.exe"
Name: "{autodesktop}\HackPDM"; Filename: "{app}\HackPDM.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\HackPDM.exe"; Description: "Start HackPDM"; Flags: nowait postinstall skipifsilent
