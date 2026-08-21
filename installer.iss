#define MyAppName "Tokenometer"
#define MyAppExeName "Tokenometer.exe"

; The version is read out of the published exe rather than repeated here, so
; <Version> in Tokenometer.csproj is the single place a release gets bumped.
; This means `dotnet publish` must run before ISCC — if publish\ is missing,
; the compile fails here instead of quietly shipping a stale version number.
#define MyAppVersion GetVersionNumbersString("publish\" + MyAppExeName)

[Setup]
AppId={{E42D1456-CD03-4A78-B63C-2BF078D4882C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer-output
OutputBaseFilename=TokenometerSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Launch {#MyAppName} automatically when I sign in"; GroupDescription: "Additional options:"; Flags: checkedonce

[InstallDelete]
; Inno only adds and overwrites — a file that has left the package stays on disk
; from the previous install. These two shipped up to 0.2.0, when the WebView2 WPF
; assembly was still referenced; nothing loads them now.
Type: files; Name: "{app}\Microsoft.Web.WebView2.Wpf.dll"
Type: files; Name: "{app}\Microsoft.Web.WebView2.Wpf.xml"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent
