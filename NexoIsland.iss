; Inno Setup script untuk NexoIsland
; Taruh file ini sejajar dengan NexoIsland.csproj dan icon.ico,
; pastikan sudah menjalankan: dotnet publish -c Release -o publish

#define MyAppName "NexoIsland"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "class_programmer/farez"
#define MyAppExeName "NexoIsland.exe"

[Setup]
AppId={{7A1E4C90-2B6D-4F38-9A57-C0D3E8B21F64}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=NexoIsland_Setup_v1
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Pasang untuk user saat ini (tidak perlu hak admin)
PrivilegesRequired=lowest
; Tutup NexoIsland yang sedang jalan saat update/uninstall
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Buat shortcut di Desktop"; GroupDescription: "Shortcut:"; Flags: unchecked
Name: "startup"; Description: "Jalankan NexoIsland otomatis saat Windows menyala"; GroupDescription: "Opsi:"

[Files]
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Jalan otomatis saat login Windows (dihapus saat uninstall)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Jalankan {#MyAppName} sekarang"; Flags: nowait postinstall skipifsilent
