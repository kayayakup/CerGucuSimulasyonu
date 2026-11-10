
#define MyAppName "CerGucuSimulasyonu"
#define MyAppVersion "1.0.0"
#define MyAppExeName "CerGucuSimulasyonu.exe"

[Setup]
AppId={{A7B31D62-4C92-4F11-9E63-81C9D8B51A20}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Installer
OutputBaseFilename=CerGucuSimulasyonu_Setup
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kýsayolu oluþtur"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Uygulamayý baþlat"; Flags: postinstall nowait skipifsilent
