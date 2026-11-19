# สถาปัตยกรรม (สำหรับผู้พัฒนาต่อ)

## ภาพรวม

```
┌─────────────────────────────┐
│   src\ZServer.Manager       │  WinForms exe (UI ภาษาไทย)
│   Program.cs + 6 Forms      │  ── เรียกใช้ ──►
└─────────────────────────────┘
                │
┌───────────────▼───────────────────────────────────────────┐
│   src\ZServer.Core  (class library — ไม่ผูกกับ UI)         │
│                                                           │
│  ชั้นค่าตั้ง/ระบบฐาน:  ServerPaths, AppConfig, Logger      │
│  ชั้นปฏิบัติการ:       ServiceHelper, ProcessRunner,      │
│                        PortChecker, FirewallHelper        │
│  ชั้นดาวน์โหลด:        Downloader, ZipExtractor,          │
│                        VersionCatalog, UpdateChecker      │
│  ชั้น config:          ConfigWriter (httpd.conf /         │
│                        php.ini / my.ini)                  │
│  ชั้นคอมโพเนนต์:       ApacheManager, PhpManager,         │
│                        MariaDbManager                     │
│  ชั้นควบคุมรวม:        StackController (เริ่ม/หยุด/สถานะ) │
└───────────────────────────────────────────────────────────┘
                │
┌───────────────▼──────────────────────┐
│  tools\ZServer.Installer (console)   │  ติดตั้งผ่าน CLI (scripting)
│  tools\ZServer.SelfTest (console)    │  ทดสอบ Core ทั้งหมดอัตโนมัติ
└──────────────────────────────────────┘
```

## โมดูลสำคัญ

| ไฟล์ | ความรับผิดชอบ |
|---|---|
| `ServerPaths.cs` | รวมทุก path ไว้ที่เดียว + ระบบค้นหารากอัตโนมัติ (`Discover()`: env `ZSERVER_HOME` → เดินขึ้นหา `zserver.json` → `C:\zServer`) |
| `AppConfig.cs` | โมเดลค่าตั้ง + serialize `zserver.json` (System.Text.Json, ทน comment/trailing comma) + `InstalledVersion()` |
| `Logger.cs` | log thread-safe ลง `logs\zserver-manager.log` + event `OnLine` ให้ UI โชว์สด |
| `PortChecker.cs` | ทดสอบ bind พอร์ตจริง + parse `netstat -ano` หาตัวร้าว + แปลเป็นข้อความไทย |
| `ProcessRunner.cs` | รัน exe จับ stdout/stderr พร้อม timeout + `StartDetached` + `KillTree` (taskkill /T) |
| `ServiceHelper.cs` | สถานะ/เริ่ม/หยุด Windows Service (System.ServiceProcess) + sc config/delete |
| `FirewallHelper.cs` | netsh เปิด/ลบ rule พอร์ต |
| `Downloader.cs` | ดาวน์โหลดพร้อม progress/ความเร็ว + **ตรวจ SHA256 หลังปิด stream แล้ว** + โหลดเป็น .part ก่อนย้าย |
| `ZipExtractor.cs` | แตก zip ป้องกัน Zip Slip + `FindSingleRoot` (php/mariadb zip) + `FindContentRoot` (zip ApacheLounge ที่มีไฟล์แถม Apache24/ReadMe/marker) + `MoveContents` |
| `ConfigWriter.cs` | เทมเพลต config ทั้งสาม — ใช้ raw string literal (`$$"""`) ใส่ literal `${SRVROOT}`/`%{Referer}i` ได้ตรง ๆ + `BackupConfig` สำรองก่อนเขียน + managed block ของ php.ini (แทรก/ตัดซ้ำด้วย marker) |
| `VersionCatalog.cs` | เวอร์ชันที่รองรับ + URL เว็บทางการ + SHA256 (URL ตรวจสอบจริง 2026-11-19) |
| `UpdateChecker.cs` | ดึงเวอร์ชันล่าสุด: PHP=releases.json / Apache=parse หน้า ApacheLounge / MariaDB=parse archive + `CompareVersions` |
| `ComponentManagers.cs` | ติดตั้ง/อัพเดทแต่ละตัว: ดาวน์โหลด → หยุดเซอร์วิส → สำรอง → แตก → เขียน config → (Apache: httpd -t; MariaDB: install-db) → ลงทะเบียน service |
| `StackController.cs` | สถานะ/เริ่ม/หยุด/รีสตาร์ท รองรับ 2 โหมด (service/process) + pid file + ตรวจพอร์ตก่อนเริ่ม + cache เวอร์ชัน 60 วิ |

## แบบแผนสำคัญ (conventions)

- **Stable paths**: ติดตั้งลง `apache\` / `php\` / `mariadb\` เสมอ (ไม่ใช่เวอร์ชันในชื่อโฟลเดอร์) เพื่อให้เซอร์วิสและ config ไม่ต้องแก้ path ทุกครั้งที่อัพเดท — โฟลเดอร์เก่าย้ายไป `backups\` ก่อนแทนที่
- **config regeneration**: ConfigWriter เป็นคนเดียวที่เขียน config; ทุกการเขียนสำรองไว้ก่อน (`BackupConfig`) และมี "จุดของผู้ใช้" ที่โปรแกรมไม่แตะ: `conf/zserver-*.conf` + ส่วนนอก managed block ของ php.ini
- **Apache บน Windows**: ต้องระบุ ServerRoot ชัดเจนทุก entry point เพราะ binary ของ ApacheLounge มี default path อื่นในตัว:
  - ทดสอบ: `httpd -t -d "<root>" -f "<conf>"`
  - เซอร์วิส: `httpd -k install -n <ชื่อ> -d <root> -f <conf>`
  - โหมด process: `httpd -d <root> -f <conf>`
- **Modules ใน httpd.conf**: โหลดเฉพาะโมดูลที่มีไฟล์ .so จริงใน `apache\modules\` — build ใหม่ของ ApacheLounge คอมไพล์ mod_mpm_winnt รวมใน httpd.exe แล้ว (LoadModule ไฟล์ที่ไม่มีจะพังทันที) และ `AddOutputFilterByType` ต้องมี mod_filter โหลดไว้ (ไม่ใช่ mod_deflate แล้วตั้งแต่ 2.3.7)
- **MariaDB service**: `mariadbd --install <ชื่อ> --defaults-file="<my.ini>"` (หรือ `--install-manual`)
- **Threading ใน UI**: งานหนักใช้ `async void` + `Task.Run` + `Progress<T>` (post กลับ UI thread เอง) + flag `_busy` กันซ้อน

## การ publish

```bash
dotnet publish src/ZServer.Manager/ZServer.Manager.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true -o bin
```
- ผลลัพธ์: `bin\ZServerManager.exe` (~50 MB) — single-file self-contained ไม่ต้องมี .NET runtime บนเครื่องปลายทาง
- app.manifest กำหนด `requireAdministrator` (ต้องการควบคุม service/firewall)

## การทดสอบ

`tools\ZServer.SelfTest` — console app ทดสอบ Core โดยไม่ใช้สิทธิ์ admin:

```bash
dotnet run --project tools/ZServer.SelfTest          # ทดสอบออฟไลน์ (35 ข้อ)
dotnet run --project tools/ZServer.SelfTest -- --net # + ดาวน์โหลดจริงทั้ง 3 ตัว + httpd -t + install-db
dotnet run --project tools/ZServer.SelfTest -- --net --keep  # ไม่ลบ sandbox (ตรวจไฟล์ที่สร้าง)
```

ครอบคลุม: round-trip ค่าตั้ง, config ทั้งสามไฟล์ตรงตามค่า, zip/zip-slip/staging, พอร์ต, เทียบเวอร์ชัน, logger, HEAD ทุก URL ในแคตตาล็อก, parser ตรวจเวอร์ชันจริง, PHP ดาวน์โหลด+SHA256+`php -v`+php.ini ซ้ำไม่ซ้อน, MariaDB install-db จริง, Apache `httpd -t` ผ่านจริง

## จุดที่ควรรู้ก่อนแก้โค้ด

1. **เวอร์ชันใหม่ของแคตตาล็อก** — แก้ที่ `VersionCatalog` (URL+SHA256); parser ของ UpdateChecker แยกอิสระ เว็บเปลี่ยนรูปแบบหน้าก็แก้เฉพาะจุดนั้น
2. **เทมเพลต config** — ระวัง brace: ใช้ `$"""` เมื่อไม่มี literal `{`; ใช้ `$$"""` + `{{expr}}` เมื่อมี (เช่น `${SRVROOT}`)
3. **ZipExtractor.FindContentRoot** — zip ของ ApacheLounge มีหลายราก ห้ามใช้ FindSingleRoot กับ Apache
4. **php.ini managed block** — marker อยู่ที่ `ServerPaths.PhpIniBeginMarker/EndMarker` ต้องคงรูปเดิมเพราะใช้ regex ตัดก่อนเขียนใหม่
5. **โหมด process** — pid เก็บใน `logs\*.pid`; Apache บน Windows เป็น single-process (mpm_winnt) จึง kill tree พอ
6. **นโยบายสำรอง** — ห้ามเขียนทับไฟล์ config โดยไม่เรียก `BackupConfig` ก่อน (กฎของ ConfigWriter)
