; ============================================================
; zServer Manager — สคริปต์สร้างตัวติดตั้ง (Inno Setup 6)
; สร้าง:  iscc installer\zserver-setup.iss
; ผลลัพธ์: installer\Output\zServer-Setup-<เวอร์ชัน>.exe
; ============================================================

#define MyAppName "zServer Manager"
#define MyAppVersion "1.4.0"
#define MyAppPublisher "zServer"
#define MyAppExeName "ZServerManager.exe"

[Setup]
; รันด้วยสิทธิ์ admin เพราะโปรแกรมหลักต้องควบคุม Windows Service
PrivilegesRequired=admin
AppId={{7E1F4A62-9C3B-4D8E-B2A1-5F6D8C90E123}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\zServer
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
; เปลี่ยนโฟลเดอร์ติดตั้งได้ในหน้า wizard (ค่าเริ่มต้น C:\zServer ตามที่เอกสารอ้างอิง)
OutputDir=Output
OutputBaseFilename=zServer-Setup-{#MyAppVersion}
SetupIconFile=..\src\ZServer.Manager\assets\zserver.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\bin\{#MyAppExeName}

[Messages]
; คำอธิบายสั้น ๆ บนหน้า wizard
SetupAppTitle=ติดตั้ง zServer Manager
SetupWindowTitle=ติดตั้ง zServer Manager {#MyAppVersion}

[Tasks]
Name: "desktopicon"; Description: "สร้างทางลัดบน Desktop"; GroupDescription: "ทางลัด:"

[Files]
; โปรแกรมหลัก (single-file self-contained — ไม่ต้องติดตั้ง .NET)
Source: "..\bin\ZServerManager.exe"; DestDir: "{app}\bin"; Flags: ignoreversion
; เอกสารคู่มือภาษาไทย
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
; โลโก้ (ไว้ใช้อ้างอิง)
Source: "..\docs\logo.png"; DestDir: "{app}\assets"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\zServer Manager"; Filename: "{app}\bin\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\zServer Manager"; Filename: "{app}\bin\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\bin\{#MyAppExeName}"; Description: "เปิด zServer Manager ตอนนี้เลย"; Flags: nowait postinstall skipifsilent

; หมายเหตุการถอนติดตั้ง: ถอนเฉพาะไฟล์ที่ setup ติดตั้ง —
; เซอร์วิส ZServerApache/ZServerMariaDB และข้อมูลใน www\ data\ backups\ logs\ downloads\ จะยังอยู่
; (ถอนเซอร์วิสผ่านโปรแกรม: ตั้งค่า > เซอร์วิส > ถอนเซอร์วิส — จึงไม่กระทบ stack ที่ใช้งานอยู่โดยไม่ตั้งใจ)
