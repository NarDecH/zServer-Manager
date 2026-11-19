# zServer Manager — ภาพรวมโปรเจกต์

![โลโก้ zServer Manager](logo.png)

zServer Manager เป็นโปรแกรมควบคุมชุดเว็บเซิร์ฟเวอร์ **Apache + PHP + MariaDB** บน Windows แบบครบวงจรในไฟล์ `.exe` เดียว ครอบคลุม 4 ภารกิจหลัก:

| ภารกิจ | ความสามารถ |
|---|---|
| **ติดตั้ง** | ดาวน์โหลดจากเว็บทางการ (Apache Lounge / windows.php.net / archive.mariadb.org) → ตรวจ SHA256 → แตกไฟล์ → เขียน config ให้เข้ากันเอง → สร้างโฟลเดอร์ข้อมูลฐานข้อมูล → ติดตั้งเป็น Windows Service |
| **ปรับแต่งค่า** | ฟอร์มตั้งค่าภาษาไทย (พอร์ต, DocumentRoot, memory_limit, extensions, timezone, buffer pool ฯลฯ) + แก้ไฟล์ config ดิบได้ + สำรองไฟล์เดิมทุกครั้ง |
| **อัพเดท** | ตรวจเวอร์ชันล่าสุดจากเว็บทางการ → อัพเดทอย่างปลอดภัย (สำรองโฟลเดอร์เดิม, ข้อมูล `data\` ไม่ถูกแตะ, กลับคืน (rollback) ได้) |
| **เปิด/ปิดเซอร์วิส** | Windows Service หรือ Process mode, tray icon, ตรวจพอร์ตชนพร้อมคำแนะนำไทย, เปิดพอร์ต Firewall |

## เริ่มใช้งานเร็ว (3 ขั้น)

1. ดับเบิลคลิก `bin\ZServerManager.exe` (จะมีข้อความยืนยัน UAC — กด "ใช่" เพราะโปรแกรมต้องควบคุมเซอร์วิส)
2. ตอนเปิดครั้งแรกถ้ายังไม่ติดตั้งอะไร โปรแกรมจะพาเข้าหน้า "ติดตั้ง" เลือกเวอร์ชันแล้วกด **ดาวน์โหลด + ติดตั้ง** ทีละตัว (แนะนำ: Apache → PHP → MariaDB)
3. กด **เริ่ม** แล้วเปิด [http://localhost](http://localhost) ได้เลย

## โครงสร้างโฟลเดอร์ที่โปรแกรมจัดการ

```
C:\zServer\
├── bin\ZServerManager.exe    โปรแกรมหลัก (single-file, self-contained)
├── apache\                   ตัว Apache ที่ติดตั้ง (bin\httpd.exe, conf\httpd.conf)
├── php\                      ตัว PHP (php.exe, php.ini, ext\)
├── mariadb\                  ตัว MariaDB (bin\mariadbd.exe, my.ini)
├── www\                      DocumentRoot — วางเว็บของคุณที่นี่ (มี index.php ตัวอย่างให้)
├── data\                     ข้อมูลฐานข้อมูล MariaDB (ไม่ควรแตะด้วยมือ)
├── logs\                     log รวมทุกตัว: apache-error.log, apache-access.log,
│                             php-error.log, mariadb-error.log, zserver-manager.log
├── downloads\                zip ที่โหลดมา (เก็บไว้ใช้ติดตั้งซ้ำได้ / ลบทิ้งได้)
│   └── staging\              โฟลเดอร์แตกไฟล์ชั่วคราว (โปรแกรมลบให้เอง)
├── backups\
│   ├── configs\              สำรอง httpd.conf / php.ini / my.ini ก่อนเขียนทับทุกครั้ง
│   └── apache-old-<เวลา>\    โฟลเดอร์เวอร์ชันเก่าก่อนอัพเดท (rollback ได้)
├── src\                      ซอร์สโค้ด C# (.NET 10 WinForms)
├── tools\ZServer.SelfTest\   โปรแกรมทดสอบตรรกะทั้งหมดอัตโนมัติ
├── docs\                     เอกสารภาษาไทยทั้งหมด
└── zserver.json              ไฟล์ค่าตั้งของโปรแกรม (แก้ผ่านหน้า "ตั้งค่า" เป็นหลัก)
```

## เทคโนโลยี

- **ภาษา/แพลตฟอร์ม**: C# 13 / .NET 10 (net10.0-windows) + Windows Forms
- **การแจกจ่าย**: publish แบบ single-file self-contained (~50 MB, ไม่ต้องติดตั้ง .NET runtime)
- **สิทธิ์**: `requireAdministrator` (ต้องการเพื่อควบคุม Windows Service และเปิดพอร์ต Firewall)
- **การควบคุมเซอร์วิส**: System.ServiceProcess + ตัวติดตั้ง native ของ Apache (`httpd -k install`) และ MariaDB (`mariadbd --install`)

## แหล่งดาวน์โหลดที่โปรแกรมใช้ (เว็บทางการเท่านั้น)

| คอมโพเนนต์ | แหล่ง |
|---|---|
| Apache 2.4.x (Win64, VS17/VS18) | https://www.apachelounge.com/download/ |
| PHP 8.x (Win32, Thread Safe x64) | https://windows.php.net/downloads/releases/ |
| MariaDB (winx64 zip) | https://archive.mariadb.org/ |
| VC++ Redistributable | https://aka.ms/vs/18/release/vc_redist.x64.exe |

ไฟล์ทั้งหมดตรวจสอบ SHA256 อัตโนมัติเมื่อแคตตาล็อกระบุค่ามา และติดตั้งจากเว็บทางการเท่านั้น

## เอกสารทั้งชุด

1. [INSTALL_TH.md](INSTALL_TH.md) — ติดตั้งทีละขั้น
2. [USER_GUIDE_TH.md](USER_GUIDE_TH.md) — คู่มือใช้งานทุกหน้าจอ
3. [CONFIG_REFERENCE_TH.md](CONFIG_REFERENCE_TH.md) — อ้างอิงไฟล์ config ทั้งหมด
4. [TROUBLESHOOTING_TH.md](TROUBLESHOOTING_TH.md) — แก้ปัญหาที่พบบ่อย
5. [ARCHITECTURE_TH.md](ARCHITECTURE_TH.md) — โครงสร้างโค้ดสำหรับผู้พัฒนา
6. [CHANGELOG.md](CHANGELOG.md) — ประวัติเวอร์ชัน

## ความสัมพันธ์กับ XAMPP เดิมในเครื่อง

โปรแกรมนี้ออกแบบให้ใช้แทนหรืออยู่ร่วมกับ XAMPP ได้:

- ไม่แตะต้องไฟล์หรือเซอร์วิสของ XAMPP เด็ดขาด
- ถ้า XAMPP กำลังรัน (ใช้พอร์ต 80/3306) โปรแกรมจะตรวจพบและแจ้งว่าใครใช้พอร์ตพร้อมวิธีแก้
- แนะนำให้เลือกอย่างใดอย่างหนึ่ง: (ก) หยุด/ถอนเซอร์วิสของ XAMPP ถาวร แล้วใช้ zServer บนพอร์ต 80/3306 หรือ (ข) เปลี่ยนพอร์ตของ zServer เป็นอื่น (เช่น 8080/3307) ในหน้า "ตั้งค่า"
