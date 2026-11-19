# สถาปัตยกรรม (สำหรับผู้พัฒนาต่อ) — ฉบับ v1.5.0

## ภาพรวม

```
┌─────────────────────────────┐
│   src\ZServer.Manager       │  WinForms exe (UI ภาษาไทย, ธีมมืด/สว่าง)
│   Program + 13 Forms        │  ── เรียกใช้ ──►
└─────────────────────────────┘
                │
┌───────────────▼───────────────────────────────────────────┐
│   src\ZServer.Core  (class library — ไม่ผูกกับ UI)         │
│                                                           │
│  ชั้นค่าตั้ง/ระบบฐาน:  ServerPaths, AppConfig, Logger      │
│  ชั้นปฏิบัติการ:       ServiceHelper, ProcessRunner,      │
│                        PortChecker, FirewallHelper,       │
│                        StartupHelper (Task Scheduler)     │
│  ชั้นดาวน์โหลด:        Downloader, ZipExtractor,          │
│                        VersionCatalog, CatalogService,    │
│                        UpdateChecker                      │
│  ชั้น config:          ConfigWriter (httpd.conf /         │
│                        php.ini / my.ini / zserver-vhosts) │
│  ชั้นคอมโพเนนต์:       ApacheManager, PhpManager,         │
│                        MariaDbManager, PhpVersionManager  │
│  ชั้น HTTPS:           HttpsHelper (mkcert + certutil +   │
│                        นำเข้าใบรับรองจริง + GetCertInfo)   │
│  ชั้นฐานข้อมูล:        MariaDbManager (CreateDatabase,    │
│                        DumpAll), DbBackupScheduler,       │
│                        SecretProtector (DPAPI)            │
│  ชั้นเว็บไซต์:         HostsHelper (hosts file)           │
│  ชั้นวิเซต:            AccessLogStats, LogRotator         │
│  ชั้นควบคุมรวม:        StackController (เริ่ม/หยุด/สถานะ) │
└───────────────────────────────────────────────────────────┘
                │
┌───────────────▼──────────────────────┐
│  tools\ZServer.Installer (console)   │  ติดตั้งผ่าน CLI (scripting)
│  tools\ZServer.SelfTest (console)    │  ทดสอบ Core ทั้งหมดอัตโนมัติ
└──────────────────────────────────────┘
```

UI (13 forms): MainForm, InstallForm, UpdateForm, SettingsForm, DbWizardForm, NewSiteForm, HttpsManagerForm, ImportCertForm, ConfigEditorForm, LogsForm, AccessSummaryForm, PasswordDialog + Theme.cs (ธีมมืด/สว่าง) + ProgressLogFilter (กรอง log ไม่ให้ล้น)

## โมดูลสำคัญ

| ไฟล์ | ความรับผิดชอบ |
|---|---|
| `ServerPaths.cs` | รวมทุก path ไว้ที่เดียว + ระบบค้นหารากอัตโนมัติ (`Discover()`: env `ZSERVER_HOME` → เดินขึ้นหา `zserver.json` → `C:\zServer`) + `EnsureDirectories()` รวม `apache\logs\` |
| `AppConfig.cs` | โมเดลค่าตั้ง (ทุก field ดู CONFIG_REFERENCE) + serialize `zserver.json` (CRLF) + `InstalledVersion()` |
| `Logger.cs` | log thread-safe ลง `logs\zserver-manager.log` + event `OnLine` ให้ UI โชว์สด |
| `PortChecker.cs` | ทดสอบ bind พอร์ตจริง + parse `netstat -ano` หาตัวร้าว + `SuggestFreePort` + ข้อความไทย |
| `ProcessRunner.cs` | รัน exe จับ stdout/stderr พร้อม timeout + `StartDetached` + `KillTree` (taskkill /T) |
| `ServiceHelper.cs` | สถานะ/เริ่ม/หยุด Windows Service (System.ServiceProcess) + sc config/delete |
| `FirewallHelper.cs` | netsh เปิด/ลบ rule พอร์ต |
| `StartupHelper.cs` | เปิดโปรแกรมอัตโนมัติตอน logon ผ่าน **Task Scheduler** (`schtasks /sc onlogon /rl highest /it`) — แอป requireAdministrator ใช้ Startup folder ไม่ได้ |
| `Downloader.cs` | ดาวน์โหลดพร้อม progress/ความเร็ว + **ตรวจ SHA256 หลังปิด stream แล้ว** + โหลดเป็น .part ก่อนย้าย |
| `ZipExtractor.cs` | แตก zip ป้องกัน Zip Slip + `FindSingleRoot` (php/mariadb zip) + `FindContentRoot` (zip ApacheLounge หลายราก) + `MoveContents` |
| `ConfigWriter.cs` | เทมเพลต config ทั้งสาม + vhosts + https block ต่อ vhost — **เขียน CRLF เสมอ** + กรอง LoadModule ตามไฟล์ .so จริง + `BackupConfig` สำรองก่อนเขียนทับ + managed block ของ php.ini |
| `VersionCatalog.cs` | เวอร์ชัน pin ไว้ (URL+SHA256 ตรวจแล้ว) + `LocalZipName` |
| `CatalogService.cs` | รวม catalog pin + **เวอร์ชันดึงสดจากเว็บ** (PHP releases.json มี SHA256 / ApacheLounge page / MariaDB archive / `RemoteCatalogUrl` ของผู้ใช้) — cache 10 นาที |
| `UpdateChecker.cs` | เวอร์ชันล่าสุด per component + `CompareVersions` |
| `ComponentManagers.cs` | ติดตั้ง/อัพเดทแต่ละตัว: ดาวน์โหลด → หยุดเซอร์วิส → สำรอง → แตก → เขียน config → (Apache: httpd -t; PHP: ระบบหลายเวอร์ชัน; MariaDB: install-db) → ลงทะเบียน service — `Report()` log เฉพาะหมุดหมาย (กัน log ล้น) |
| `PhpVersionManager.cs` | ระบบ PHP หลายเวอร์ชันด้วย **junction**: เวอร์ชันเก็บที่ `php-versions\{ver}\`, `php\` เป็น junction ชี้เวอร์ชัน active — `EnsureVersionsLayout` (ย้าย php\ เดิมเข้าระบบ) + `SwitchTo` + `ListVersions` + `ActiveVersion` |
| `HttpsHelper.cs` | mkcert (โหลดจาก GitHub API + generate — **ไม่ใช้ mkcert -install เพราะค้างบางเครื่อง**) + **ติดตั้ง CA ผ่าน `certutil -addstore Root`** + `GetCertInfo` (openssl อ่าน subject/expiry/SAN) + **`ImportCommercialCert`** (แปลง PEM/DER, pkey RSA/EC+รหัสผ่าน, pkcs12 pfx, ตรวจ public key ตรง cert, ต่อ chain) |
| `DbBackupScheduler.cs` | สำรองอัตโนมัติทุกวันตามเวลา (timer 1 นาทีเรียก `CheckAndRun` — วิ่งวันละครั้ง) + retention + lock กันซ้อน |
| `SecretProtector.cs` | เข้ารหัส/ถอดรหัสรหัสผ่านด้วย **DPAPI** (CurrentUser — ถอดได้เฉพาะเครื่องนี้) |
| `HostsHelper.cs` | ซิงก์ vhost เข้า hosts file — **แก้เฉพาะ section มี marker** `# ---- zServer vhosts BEGIN/END ----` |
| `AccessLogStats.cs` | parse combined log format → Top IP/path/status (cap 300k บรรทัด) |
| `LogRotator.cs` | log เก่ากว่า N วัน → บีบอัด `backups\log-archives\` → ลบ (ไฟล์ล็อคข้าม) |
| `StackController.cs` | สถานะ/เริ่ม/หยุด/รีสตาร์ท 2 โหมด (service/process) + pid file + ตรวจพอร์ตก่อนเริ่ม + cache เวอร์ชัน 60 วิ |

## Manager forms (UI)

| Form | หน้าที่ |
|---|---|
| `MainForm` | แดชบอร์ด + tray + watchdog (30 วิ) + backup timer (1 นาที) + update check (6 ชม.) + log สด (autoscroll ฉลาด) + ป้ายเวอร์ชันใหม่ |
| `InstallForm` | ติดตั้งคอมโพเนนต์ + Adminer + VC redist + port auto-suggest + เตือนรหัส root |
| `SettingsForm` | 6 แท็บ (ทั่วไป/Apache/PHP/MariaDB/Virtual Hosts/เซอร์วิส) + autostart sync + PasswordDialog |
| `UpdateForm` | ตรวจ/อัพเดทเวอร์ชัน |
| `DbWizardForm` | สร้าง DB+user+grant + connection string + รายการ databases |
| `NewSiteForm` | สร้างเว็บจากเทมเพลต (เว็บเปล่า/WordPress) + DB + vhost + hosts |
| `HttpsManagerForm` | จัดการ HTTPS 3 ขั้น + สถานะจริงจาก openssl |
| `ImportCertForm` | นำเข้าใบรับรองจริง (file input + ตรวจทุกขั้น) |
| `ConfigEditorForm` | แก้ config ดิบ (แปลง CRLF ตอนแสดง) |
| `LogsForm` | ดู log + AccessSummaryForm |
| `AccessSummaryForm` | สถิติ access.log |
| `Theme.cs` | ธีมมืด/สว่าง (walk control แบบเจาะจงประเภท) |
| `ProgressLogFilter.cs` | กรอง progress ไม่ให้ log ล้น (บันทึกหมุดหมาย 10%) |

## แบบแผนสำคัญ (conventions)

- **Stable paths + junction PHP**: ติดตั้งลง `apache\` / `php\` / `mariadb\` เสมอ — PHP หลายเวอร์ชันใช้ junction: เวอร์ชันเก็บที่ `php-versions\{ver}\`, `php\` เป็น junction ชี้เวอร์ชัน active (config อ้างอิง `php\` เสมอ จึงไม่แตะเวลาสลับ) — สร้าง junction ผ่าน `cmd /c mklink /J`
- **Modules ใน httpd.conf**: โหลดเฉพาะโมดูลที่มีไฟล์ .so จริงใน `apache\modules\` — build ใหม่ของ ApacheLounge คอมไพล์ mod_mpm_winnt รวมใน httpd.exe แล้ว (LoadModule ไฟล์ที่ไม่มีจะพังทันที) + `AddOutputFilterByType` ต้องมี mod_filter โหลดไว้ (ไม่ใช่ mod_deflate แล้วตั้งแต่ 2.3.7)
- **Apache บน Windows**: ระบุ ServerRoot ชัดเจนทุก entry point (`-t -d root -f conf`, `-k install -n name -d root -f conf`, console `-d root -f conf`)
- **mkcert**: ไม่ใช้ `mkcert -install` (ค้างบางเครื่อง) — mkcert สร้าง CA เองตอน generate แล้วติดตั้ง trust ผ่าน `certutil -addstore -f Root rootCA.pem` (ยืนยัน https://localhost ตอบ 200 โดย schannel เชื่อจริง)
- **CRLF**: ConfigWriter/AppConfig เขียนไฟล์เป็น CRLF เสมอ (กัน Notepad/TextBox แสดงบรรทัดเดียว)
- **config regeneration**: ConfigWriter เป็นคนเดียวที่เขียน config; ทุกการเขียนสำรองไว้ก่อน (`BackupConfig`) — จุดของผู้ใช้ที่โปรแกรมไม่แตะ: `conf/zserver-*.conf` + ส่วนนอก managed block ของ php.ini
- **รหัสผ่านลับ**: เก็บผ่าน DPAPI (`SecretProtector`) — ไม่เก็บ plaintext ใน config
- **hosts file**: แก้เฉพาะ section marker `# ---- zServer vhosts BEGIN/END ----` + สำรองเดิมก่อนเสมอ
- **Threading ใน UI**: งานหนักใช้ `async void` + `Task.Run` + `Progress<T>` + flag `_busy` กันซ้อน; watchdog มี suppress list กันแจ้งตอนผู้ใช้สั่งหยุดเอง
- **Watchdog**: cache สถานะตัวเอง + ติ๊กแรกจดสถานะ (กัน false alarm ตอนเปิดโปรแกรม) + `_watchdogSuppress` ตอนผู้ใช้สั่งหยุด

## การ publish + release pipeline

```bash
# publish exe
dotnet publish src/ZServer.Manager/ZServer.Manager.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true -o bin

# สร้าง Setup.exe (ต้องมี Inno Setup 6)
"%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer/zserver-setup.iss

# ปล่อย release (CI ทำให้อัตโนมัติ)
git tag v1.5.1 && git push origin v1.5.1
# → build job + release job (choco install innosetup + ISCC + gh release create --draft)
# → draft release รอผู้ดูแลกด Publish เองบน GitHub
```

- workflow: `.github\workflows\build.yml` — build ทุก push / release เมื่อ tag `v*` (idempotent: ถ้า draft มีอยู่แล้ว `gh release upload --clobber`)
- ปล่อยเวอร์ชันใหม่: bump `<Version>` ใน csproj + `#define MyAppVersion` ใน iss + CHANGELOG + tag

## การทดสอบ

`tools\ZServer.SelfTest` — console app ทดสอบ Core โดยไม่ใช้สิทธิ์ admin (35+ ข้อ):

```bash
dotnet run --project tools/ZServer.SelfTest          # ออฟไลน์
dotnet run --project tools/ZServer.SelfTest -- --net # + ดาวน์โหลดจริง + httpd -t + install-db
```

การทดสอบ E2E สำคัญที่ผ่านแล้ว (เก็บสคริปต์ไว้ใน tools\):
- **สลับ PHP หลายเวอร์ชัน**: 8.5.13 ↔ 8.6.0 จริง — `php -v` ตรงทั้งคู่ผ่าน junction (tools\php-version-test.ps1)
- **HTTPS จริง**: mkcert + certutil → Apache ให้บริการ 443 → `https://localhost` 200 โดย schannel เชื่อใบรับรอง
- **นำเข้าใบรับรองจริง**: 2-file ✓ / key ผิดถูกจับ ✓ / pfx+รหัสผ่าน ✓ (tools\import-cert-test.ps1)

## จุดที่ควรรู้ก่อนแก้โค้ด

1. **เวอร์ชันใหม่ของแคตตาล็อก** — pin ที่ `VersionCatalog` (URL+SHA256); ดึงสดผ่าน `CatalogService` (รวม remote catalog ของผู้ใช้ด้วย)
2. **เทมเพลต config** — ระวัง brace: ใช้ `$"""` เมื่อไม่มี literal `{`; ใช้ `$$"""` + `{{expr}}` เมื่อมี (เช่น `${SRVROOT}`, `%{Referer}i`)
3. **ZipExtractor.FindContentRoot** — zip ของ ApacheLounge มีหลายราก (Apache24/ + ReadMe + marker); PHP zip ไม่มีรากเลย
4. **php.ini managed block** — marker อยู่ที่ `ServerPaths.PhpIniBeginMarker/EndMarker` ใช้ regex ตัดก่อนเขียนใหม่ อย่าเปลี่ยนรูป marker
5. **โหมด process** — pid เก็บใน `logs\*.pid`; Apache บน Windows เป็น single-process (mpm_winnt) จึง kill tree พอ
6. **นโยบายสำรอง** — ห้ามเขียนทับไฟล์ config โดยไม่เรียก `BackupConfig` ก่อน (กฎของ ConfigWriter)
7. **CRLF** — ไฟล์ที่เขียนผ่าน ConfigWriter/AppConfig เป็น CRLF เสมอ (กัน TextBox/Notepad แสดงบรรทัดเดียว)
8. **Watchdog suppress** — ตอนผู้ใช้สั่งหยุด/รีสตาร์ท ต้องส่งชื่อคอมโพเนนต์ใน `RunAsync(..., "apache")` ไม่งั้น watchdog จะแจ้งว่าล้ม
