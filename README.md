# zServer Manager

[![build](https://github.com/NarDecH/zServer-Manager/actions/workflows/build.yml/badge.svg)](https://github.com/NarDecH/zServer-Manager/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/NarDecH/zServer-Manager?label=release&color=2ea44f)](https://github.com/NarDecH/zServer-Manager/releases)
[![downloads](https://img.shields.io/github/downloads/NarDecH/zServer-Manager/total?label=downloads&color=blue)](https://github.com/NarDecH/zServer-Manager/releases)
[![license](https://img.shields.io/github/license/NarDecH/zServer-Manager?color=orange)](LICENSE)

![zServer Manager](docs/banner.png)

โปรแกรมควบคุม **Apache / PHP / MariaDB** บน Windows ในที่เดียว — ติดตั้ง ตั้งค่า อัพเดท เปิด-ปิดเซอร์วิส สร้างเว็บ จัดการฐานข้อมูล และ HTTPS ได้ครบในโปรแกรมเดียว (ภาษาไทยทั้งหมด)

## โปรแกรมหลัก

| ไฟล์ | คำอธิบาย |
|---|---|
| `bin\ZServerManager.exe` | โปรแกรมหลัก (GUI + tray icon + ธีมมืด/สว่าง) — ไฟล์เดียวจบ ไม่ต้องติดตั้ง .NET เพิ่ม |
| `installer\Output\zServer-Setup-1.5.0.exe` | ตัวติดตั้ง Setup.exe (Inno Setup) — ติดตั้ง + shortcut + คู่มือ ในคลิกเดียว |
| `tools\ZServer.Installer` | ติดตั้งผ่าน command line (สำหรับ scripting) — ดู [docs/INSTALL_TH.md](docs/INSTALL_TH.md) |

ดับเบิลคลิก `bin\ZServerManager.exe` เพื่อใช้งาน (โปรแกรมต้องการสิทธิ์ผู้ดูแลระบบเพราะต้องควบคุม Windows Service)

## ความสามารถทั้งหมด

- **ติดตั้ง** — ดาวน์โหลด Apache (Apache Lounge), PHP (windows.php.net, รุ่น Thread Safe — มี 8.6.0 ล่าสุดอัตโนมัติ), MariaDB (archive.mariadb.org) จากเว็บทางการ พร้อมตรวจ SHA256, แตกไฟล์, เขียน config เชื่อมกันเอง, ติดตั้งเป็น Windows Service — และ **Adminer** จัดการฐานข้อมูลผ่านเว็บในปุ่มเดียว
- **จัดการฐานข้อมูล** — สร้าง database + user + grant ในคลิกเดียว พร้อม connection string (mysqli/PDO) คัดลอกไปใช้ได้เลย + **สำรองฐานข้อมูลอัตโนมัติทุกวัน** (ตั้งเวลาได้) + ปุ่มสำรองด่วน
- **สร้างเว็บใหม่จากเทมเพลต** — เว็บเปล่า หรือ **WordPress** (ดาวน์โหลดให้) → สร้างโฟลเดอร์ + database/user + Virtual Host + hosts file ครบในคลิกเดียว
- **Virtual Hosts** — รันหลายเว็บในเครื่องเดียว (`project.local` → โฟลเดอร์) โปรแกรมสร้าง vhost config + แก้ hosts file ให้เอง
- **HTTPS ครบวงจร** — ① สร้างใบรับรองทดสอบด้วย mkcert (ครอบคลุม localhost + vhost ทุกตัว, browser เชื่อไม่เตือน) ② เปิด/ปิด HTTPS ③ รีสตาร์ท — หรือ **นำเข้าใบรับรองจริงที่ซื้อมา** (.crt/.key/.pem + CA bundle หรือ .pfx ไฟล์เดียว) พร้อมตรวจ key ตรง cert อัตโนมัติ
- **PHP หลายเวอร์ชัน** — ติดตั้งเก็บหลายเวอร์ชันแล้ว **สลับกันใน 3 วินาที** ผ่านแท็บ PHP (ระบบ junction ไม่ต้องแก้ config)
- **ตั้งค่า** — พอร์ต, DocumentRoot, memory_limit, extensions (พร้อมคำอธิบายไทย + ตรวจไฟล์จริง), timezone, Xdebug toggle, MariaDB, โหมดเซอร์วิส — บันทึกทุกครั้งสำรองไฟล์เดิมให้ก่อน
- **อัพเดท** — แจ้งเตือนเวอร์ชันใหม่อัตโนมัติ (ทุก 6 ชม.) ตรวจจากเว็บทางการ อัพเดทแบบไม่กระทบข้อมูล (สำรองลง `backups\`, rollback ได้)
- **เปิด/ปิดเซอร์วิส** — Windows Service หรือ Process mode, tray icon, ตรวจพอร์ตชน + เสนอพอร์ตว่างอัตโนมัติ, เปิดพอร์ต Firewall, **watchdog จับเซอร์วิสล้ม** (แจ้งเตือน + เริ่มให้ใหม่อัตโนมัติได้), เปิดโปรแกรมอัตโนมัติตอน logon
- **ดูแลระบบ** — log rotation อัตโนมัติ (บีบอัด log เก่า), สรุปสถิติ access.log (Top IP/หน้าเว็บ/status), ปุ่มเปิด Terminal พร้อม PATH, คู่มือในเครื่อง
- **ธีมมืด/สว่าง** — สลับจาก tray หรือหน้าตั้งค่า

## เอกสารทั้งหมด (ภาษาไทย)

| เอกสาร | เนื้อหา |
|---|---|
| [docs/README_TH.md](docs/README_TH.md) | ภาพรวมโปรเจกต์ โครงสร้างโฟลเดอร์ เริ่มใช้งานเร็ว |
| [docs/INSTALL_TH.md](docs/INSTALL_TH.md) | การติดตั้งทีละขั้น (Setup.exe / ใช้ตรง / CLI / autostart) |
| [docs/USER_GUIDE_TH.md](docs/USER_GUIDE_TH.md) | คู่มือใช้งานทุกหน้าจอ + workflow ที่ใช้บ่อย ละเอียดทุกขั้นตอน |
| [docs/CONFIG_REFERENCE_TH.md](docs/CONFIG_REFERENCE_TH.md) | อ้างอิง zserver.json ทุก field + ไฟล์ config ที่โปรแกรมสร้างให้ |
| [docs/TROUBLESHOOTING_TH.md](docs/TROUBLESHOOTING_TH.md) | แก้ปัญหาที่พบบ่อยครบทุกกรณี |
| [docs/ARCHITECTURE_TH.md](docs/ARCHITECTURE_TH.md) | โครงสร้างโค้ดสำหรับผู้พัฒนาต่อ + release pipeline |
| [docs/CHANGELOG.md](docs/CHANGELOG.md) | ประวัติการเปลี่ยนแปลง (1.0.0 → 1.5.0) |

## โครงสร้างโฟลเดอร์หลัก

```
C:\zServer\
├── bin\ZServerManager.exe    ← โปรแกรมหลัก
├── installer\                ← ตัวติดตั้ง Setup.exe (Inno Setup script + output)
├── apache\                   ← Apache (ติดตั้งผ่านโปรแกรม)
├── php\                      ← PHP ที่ใช้งานอยู่ (junction ชี้ไปเวอร์ชัน active)
├── php-versions\8.5.13\      ← เก็บ PHP หลายเวอร์ชันไว้สลับได้
├── mariadb\                  ← MariaDB
├── www\                      ← เว็บของคุณ (DocumentRoot)
├── data\                     ← ข้อมูลฐานข้อมูล MariaDB
├── logs\                     ← log ทุกตัวรวมกันที่นี่
├── downloads\                ← ไฟล์ zip ที่ดาวน์โหลดมา (ลบได้เมื่อติดตั้งเสร็จ)
├── backups\                  ← สำรอง config/โฟลเดอร์เก่า/ฐานข้อมูล/log-archives
├── src\                      ← ซอร์สโค้ด (C# .NET 10)
├── tools\                    ← CLI installer + SelfTest + สคริปต์เจนโลโก้
├── docs\                     ← เอกสารทั้งหมด
├── .github\workflows\        ← CI: build exe อัตโนมัติทุก push
├── LICENSE                   ← MIT
└── zserver.json              ← ค่าตั้งของโปรแกรม
```

## สร้างโปรแกรมจากซอร์ส (สำหรับผู้พัฒนา)

```bash
# build ทั้งหมด
dotnet build src/ZServer.Manager/ZServer.Manager.csproj

# publish ไฟล์เดียว self-contained
dotnet publish src/ZServer.Manager/ZServer.Manager.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o bin

# ทดสอบตัวเอง (ไม่ต้อง admin)
dotnet run --project tools/ZServer.SelfTest -- --net
```

## ข้อควรรู้

- ค่าเริ่มต้นพอร์ต: **80 (HTTP) / 443 (HTTPS) / 3306 (MariaDB)** — ถ้าชนกับโปรแกรมอื่น (เช่น XAMPP) โปรแกรมจะเตือนพร้อมเสนอพอร์ตว่างให้เปลี่ยนอัตโนมัติ
- PHP ใช้รุ่น **Thread Safe** เท่านั้น (เป็นโมดูลของ Apache) — โปรแกรมเลือกให้เอง
- โปรแกรมไม่แตะต้องการตั้งค่าของ XAMPP เดิมในเครื่องแต่อย่างใด
