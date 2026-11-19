# zServer Manager — ภาพรวมโปรเจกต์

![โลโก้ zServer Manager](logo.png)

zServer Manager เป็นโปรแกรมควบคุมชุดเว็บเซิร์ฟเวอร์ **Apache + PHP + MariaDB** บน Windows แบบครบวงจรในไฟล์ `.exe` เดียว ครอบคลุมทุกภารกิจของการพัฒนาเว็บบนเครื่องตัวเอง:

| ภารกิจ | ความสามารถ |
|---|---|
| **ติดตั้ง** | ดาวน์โหลดจากเว็บทางการ (ตรวจ SHA256) → แตกไฟล์ → เขียน config เชื่อมกันเอง → สร้าง datadir → ติดตั้ง Windows Service → ติดตั้ง **Adminer** จัดการฐานข้อมูลผ่านเว็บ |
| **ปรับแต่งค่า** | ฟอร์มภาษาไทยครบทุกค่าที่ใช้บ่อย + แก้ไฟล์ config ดิบได้ + สำรองไฟล์เดิมทุกครั้ง + สลับ PHP หลายเวอร์ชัน |
| **อัพเดท** | ตรวจเวอร์ชันใหม่อัตโนมัติทุก 6 ชม. + แจ้งเตือน tray → อัพเดทอย่างปลอดภัย (สำรองก่อนเสมอ, rollback ได้) |
| **เซอร์วิส** | Windows Service / Process mode, watchdog จับเซอร์วิสล้ม, ตรวจพอร์ตชน + เสนอพอร์ตว่าง, เปิดโปรแกรมอัตโนมัติตอน logon |
| **ฐานข้อมูล** | สร้าง database + user ในคลิกเดียว (พร้อม connection string), สำรองอัตโนมัติทุกวัน, Adminer |
| **เว็บไซต์** | Virtual Hosts หลายเว็บ, สร้างเว็บใหม่จากเทมเพลต (เว็บเปล่า / WordPress) ครบทั้ง DB + โดเมน |
| **HTTPS** | ใบรับรองทดสอบ (mkcert — browser เชื่อไม่เตือน) หรือ **นำเข้าใบรับรองจริงที่ซื้อมา** (.crt/.key/.pfx) — ครอบคลุมทุกเว็บ |

## เริ่มใช้งานเร็ว (3 ขั้น)

1. ดับเบิลคลิก `bin\ZServerManager.exe` → กด UAC "ใช่" (โปรแกรมต้องการสิทธิ์ admin เพื่อควบคุมเซอร์วิส)
2. ครั้งแรกโปรแกรมจะพาเข้าหน้า "ติดตั้ง" → กดติดตั้ง **Apache → PHP → MariaDB** ทีละตัว (ดาวน์โหลดอัตโนมัติจากเว็บทางการ)
3. กด **เริ่มทั้งหมด** → เปิด [http://localhost](http://localhost) ได้เลย!

## โครงสร้างโฟลเดอร์ที่โปรแกรมจัดการ

```
C:\zServer\
├── bin\ZServerManager.exe    โปรแกรมหลัก (single-file self-contained)
├── installer\                ตัวติดตั้ง Setup.exe (Inno Setup script + Output)
│   └── Output\zServer-Setup-1.5.0.exe
├── apache\                   ตัว Apache ที่ติดตั้ง (bin\httpd.exe, conf\httpd.conf)
│   └── conf\ssl\             ใบรับรอง HTTPS (cert.pem + key.pem)
├── php\                      PHP ที่ "ใช้งานอยู่" — เป็น junction ชี้ไปเวอร์ชัน active
├── php-versions\             เก็บ PHP หลายเวอร์ชันไว้สลับ (8.5.13\, 8.6.0\ ...)
├── mariadb\                  ตัว MariaDB (bin\mariadbd.exe, my.ini)
├── www\                      DocumentRoot — วางเว็บของคุณที่นี่ (มี index.php ตัวอย่าง)
├── data\                     ข้อมูลฐานข้อมูล MariaDB (ไม่ควรแตะด้วยมือ)
├── logs\                     log รวมทุกตัว: apache-error.log, apache-access.log,
│                             php-error.log, mariadb-error.log, zserver-manager.log
├── downloads\                zip ที่โหลดมา (เก็บไว้ติดตั้งซ้ำได้ / ลบทิ้งได้)
│   └── mkcert.exe            เครื่องมือสร้างใบรับรอง (โหลดอัตโนมัติ)
├── backups\
│   ├── configs\              สำรอง httpd.conf / php.ini / my.ini ก่อนเขียนทับทุกครั้ง
│   ├── apache-old-<เวลา>\    โฟลเดอร์เวอร์ชันเก่าก่อนอัพเดท (rollback ได้)
│   ├── databases\            ไฟล์สำรองฐานข้อมูล (.sql จาก mariadb-dump)
│   ├── log-archives\         log เก่าที่ถูกบีบอัดเก็บ
│   └── hosts-*.bak           สำรอง hosts file ก่อนแก้ vhost
├── src\                      ซอร์สโค้ด C# (.NET 10 WinForms)
├── tools\
│   ├── ZServer.Installer\    ติดตั้งผ่าน CLI (สำหรับ scripting)
│   ├── ZServer.SelfTest\     โปรแกรมทดสอบตรรกะทั้งหมดอัตโนมัติ
│   └── icon\                 สคริปต์เจนโลโก้ + ดีไซน์ตัวเลือก
├── docs\                     เอกสารภาษาไทยทั้งหมด (8 ไฟล์)
├── .github\workflows\        CI: build exe อัตโนมัติทุก push
├── LICENSE                   MIT
└── zserver.json              ไฟล์ค่าตั้งของโปรแกรม (แก้ผ่านหน้า "ตั้งค่า" เป็นหลัก)
```

## เทคโนโลยี

- **ภาษา/แพลตฟอร์ม**: C# 13 / .NET 10 (net10.0-windows) + Windows Forms
- **การแจกจ่าย**: publish แบบ single-file self-contained (~50 MB, ไม่ต้องติดตั้ง .NET runtime)
- **สิทธิ์**: `requireAdministrator` (จำเป็นเพื่อควบคุม Windows Service + Firewall + hosts file)
- **CI/CD**: GitHub Actions — build exe อัตโนมัติทุก push, สร้าง Release (draft) เมื่อ push tag `v*`

## แหล่งดาวน์โหลดที่โปรแกรมใช้ (เว็บทางการเท่านั้น)

| คอมโพเนนต์ | แหล่ง |
|---|---|
| Apache 2.4.x (Win64, VS17/VS18) | https://www.apachelounge.com/download/ |
| PHP 8.x (Win32, Thread Safe x64) | https://windows.php.net/downloads/releases/ (อ่าน releases.json — ได้ SHA256 + เวอร์ชันใหม่อัตโนมัติ) |
| MariaDB (winx64 zip) | https://archive.mariadb.org/ |
| VC++ Redistributable | https://aka.ms/vs/18/release/vc_redist.x64.exe |
| mkcert | https://github.com/FiloSottile/mkcert/releases |
| Adminer | https://www.adminer.org/latest.php |

ไฟล์ทั้งหมดตรวจสอบ SHA256 อัตโนมัติเมื่อแคตตาล็อกระบุค่ามา

## เอกสารทั้งชุด

1. [INSTALL_TH.md](INSTALL_TH.md) — ติดตั้งทีละขั้น (Setup.exe / ใช้ตรง / CLI)
2. [USER_GUIDE_TH.md](USER_GUIDE_TH.md) — คู่มือใช้งานทุกหน้าจอ + workflow ละเอียด
3. [CONFIG_REFERENCE_TH.md](CONFIG_REFERENCE_TH.md) — อ้างอิงไฟล์ config ทั้งหมด
4. [TROUBLESHOOTING_TH.md](TROUBLESHOOTING_TH.md) — แก้ปัญหาที่พบบ่อย
5. [ARCHITECTURE_TH.md](ARCHITECTURE_TH.md) — โครงสร้างโค้ดสำหรับผู้พัฒนา
6. [CHANGELOG.md](CHANGELOG.md) — ประวัติเวอร์ชัน

## ความสัมพันธ์กับ XAMPP เดิมในเครื่อง

โปรแกรมนี้ออกแบบให้อยู่ร่วมกับ XAMPP ได้อย่างปลอดภัย:

- ไม่แตะต้องไฟล์หรือเซอร์วิสของ XAMPP เด็ดขาด
- ถ้า XAMPP กำลังรัน (ใช้พอร์ต 80/3306) โปรแกรมจะตรวจพบ + แจ้งว่าใครใช้พอร์ต + **เสนอพอร์ตว่างให้เปลี่ยนอัตโนมัติได้ในคลิกเดียว**
- แนะนำ: หยุดเซอร์วิส XAMPP แล้วใช้ zServer บนพอร์ตมาตรฐาน หรือเปลี่ยนพอร์ต zServer เป็น 8080/3307 ใช้คู่กันได้เลย
