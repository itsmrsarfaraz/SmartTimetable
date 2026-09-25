; ===================================================================
;  SmartTimetable AI - Inno Setup installer script
;  Product by Crafting Colons
;
;  Produces SmartTimetable-Setup.exe, which installs the single
;  self-contained SmartTimetable.exe, creates Start-Menu (and optional
;  desktop) shortcuts, and registers a proper Windows uninstaller.
;
;  ICONS
;    * The application / shortcut icon is  timetable.ico
;    * The UNINSTALL icon (the one shown in Windows "Apps & features"
;      / "Programs and Features" and on the Start-Menu uninstall entry,
;      i.e. what the user sees when they want to DELETE the app) is
;      TimetableDelete.ico  -- set via UninstallDisplayIcon below.
;
;  PREREQUISITES (build these first, on Windows):
;    1. Run publish.bat in the repo root  -> produces publish\SmartTimetable.exe
;    2. Make sure the two icon files exist (see paths in [Files] below).
;    3. Install Inno Setup 6 (https://jrsoftware.org/isdl.php), then run
;       build-installer.bat  (or open this file in the Inno Setup IDE and
;       press Build).
; ===================================================================

#define MyAppName        "SmartTimetable AI"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "Crafting Colons"
#define MyAppExeName     "SmartTimetable.exe"

; Paths are relative to THIS script (the installer\ folder).
#define AppIcon          "..\src\SmartTimetable.Desktop\Assets\timetable.ico"
#define UninstallIcon    "TimetableDelete.ico"
#define PublishedExe     "..\publish\SmartTimetable.exe"

[Setup]
; A stable AppId keeps upgrades/uninstall consistent across versions -- do NOT change it.
AppId={{A7E4D2C9-1B36-4F8A-9E52-3C7D6B0A4F81}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoVersion={#MyAppVersion}

DefaultDirName={autopf}\SmartTimetable
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=SmartTimetable-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin

; The .exe of the installer itself uses the application icon.
SetupIconFile={#AppIcon}
; The UNINSTALL entry's icon (Apps & features / Programs and Features).
UninstallDisplayIcon={app}\TimetableDelete.ico
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishedExe}";  DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "{#AppIcon}";       DestDir: "{app}"; DestName: "timetable.ico";   Flags: ignoreversion
Source: "{#UninstallIcon}"; DestDir: "{app}"; DestName: "TimetableDelete.ico"; Flags: ignoreversion

[Icons]
; Start-Menu launch shortcut -> application icon.
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\timetable.ico"
; Start-Menu uninstall shortcut -> delete icon.
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\TimetableDelete.ico"
; Optional desktop shortcut -> application icon.
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\timetable.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; NOTE: the app's own data (the timetable database in %APPDATA%\SmartTimetable and
; the license in %LOCALAPPDATA%\SmartTimetable) is intentionally left in place on
; uninstall, so a reinstall keeps the college's data and activation. Add an
; [UninstallDelete] section here if you ever want a full wipe on removal.
