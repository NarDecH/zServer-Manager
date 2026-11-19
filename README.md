# zServer Manager

[![build](https://github.com/NarDecH/zServer-Manager/actions/workflows/build.yml/badge.svg)](https://github.com/NarDecH/zServer-Manager/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/NarDecH/zServer-Manager?label=release&color=2ea44f)](https://github.com/NarDecH/zServer-Manager/releases)
[![downloads](https://img.shields.io/github/downloads/NarDecH/zServer-Manager/total?label=downloads&color=blue)](https://github.com/NarDecH/zServer-Manager/releases)

![zServer Manager](docs/banner.png)

โปรแกรมควบคุม Apache / PHP / MariaDB บน Windows ในที่เดียว — ติดตั้ง ตั้งค่า อัพเดท เปิด-ปิดเซอร์วิส

![สถานะ](https://img.shields.io/badge/version-1.0.0-green) ![แพลตฟอร์ม](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)

## โปรแกรมหลัก

| ไฟล์ | คำอธิบาย |
|---|---|
| `bin\ZServerManager.exe` | โปรแกรมหลัก (GUI + tray icon + ธีมมืด/สว่าง) — ไฟล์เดียวจบ ไม่ต้องติดตั้ง .NET เพิ่ม |
| `installer\Output\zServer-Setup-1.1.0.exe` | ตัวติดตั้ง Setup.exe (Inno Setup) — ติดตั้ง + shortcut + คู่มือ ในคลิกเดียว |
| `tools\ZServer.Installer` | ติดตั้งผ่าน command line (สำหรับ scripting) — ดู [docs/INSTALL_TH.md](docs/INSTALL_TH.md) |

ดับเบิลคลิก `bin\ZServerManager.exe` เพื่อใช้งาน (โปรแกรมต้องการสิทธิ์ผู้ดูแลระบบเพราะต้องควบคุม Windows Service)

## ความสามารถ

- **ติดตั้ง** — ดาวน์โหลด Apache (Apache Lounge), PHP (windows.php.net, รุ่น Thread Safe), MariaDB (archive.mariadb.org) จากเว็บทางการ แตกไฟล์ เขียน config เชื่อมกันอัตโนมัติ สร้าง datadir และติดตั้งเป็น Windows Service ได้ในคลิกเดียว — พร้อม **Adminer** จัดการฐานข้อมูลผ่านเว็บในปุ่มเดียว
- **ตั้งค่า** — แก้ค่าที่ใช้บ่อยผ่านฟอร์มภาษาไทย (พอร์ต, DocumentRoot, memory_limit, extension พร้อมคำอธิบาย, timezone, HTTPS, bind-address ฯลฯ) พร้อมแก้ไฟล์ config ดิบ ๆ ได้ ทุกการบันทึกสำรองไฟล์เดิมให้ก่อนเสมอ
- **Virtual Hosts** — เพิ่มหลายเว็บในเครื่องเดียว (`project.local` → โฟลเดอร์) โปรแกรมสร้าง vhost config + แก้ hosts file ให้เอง
- **HTTPS** — สร้างใบรับรองทดสอบด้วย mkcert (browser เชื่อไม่เตือน) และเปิด mod_ssl ให้ในคลิกเดียว
- **อัพเดท** — แจ้งเตือนเวอร์ชันใหม่อัตโนมัติ (ตรวจทุก 6 ชม. + ตอนเปิดโปรแกรม) ตรวจจากเว็บทางการ อัพเดทแบบไม่กระทบข้อมูล (สำรองลง `backups\`, ข้อมูลใน `data\` ไม่ถูกแตะ)
- **เปิด/ปิดเซอร์วิส** — รองรับ 2 โหมด: Windows Service และ Process mode, tray icon ควบคุมด่วน, ตรวจพอร์ตชน + เสนอพอร์ตว่างอัตโนมัติ, เปิดพอร์ต Firewall, เปิดอัตโนมัติตอน logon
- **ดูแลระบบ** — log rotation อัตโนมัติ (บีบอัดเก็บ log เก่า), สำรอง config ทุกครั้งก่อนเขียนทับ, MariaDB ฟัง localhost เท่านั้นโดยค่าเริ่มต้น + แนะนำตั้งรหัสผ่าน root, ปุ่มเปิด Terminal พร้อม PATH

## เอกสารทั้งหมด (ภาษาไทย)

| เอกสาร | เนื้อหา |
|---|---|
| [docs/README_TH.md](docs/README_TH.md) | ภาพรวมโปรเจกต์ โครงสร้างโฟลเดอร์ เริ่มใช้งานเร็ว |
| [docs/INSTALL_TH.md](docs/INSTALL_TH.md) | การติดตั้งทีละขั้น (โปรแกรม + stack + VC++ Redistributable + Firewall) |
| [docs/USER_GUIDE_TH.md](docs/USER_GUIDE_TH.md) | คู่มือใช้งานทุกหน้าจอ + workflow ที่ใช้บ่อย |
| [docs/CONFIG_REFERENCE_TH.md](docs/CONFIG_REFERENCE_TH.md) | อ้างอิง zserver.json + ไฟล์ config ที่โปรแกรมสร้างให้ |
| [docs/TROUBLESHOOTING_TH.md](docs/TROUBLESHOOTING_TH.md) | แก้ปัญหาพอร์ตชน เซอร์วิสติดตั้งไม่ได้ ฯลฯ |
| [docs/ARCHITECTURE_TH.md](docs/ARCHITECTURE_TH.md) | โครงสร้างโค้ดสำหรับผู้พัฒนาต่อ |
| [docs/CHANGELOG.md](docs/CHANGELOG.md) | ประวัติการเปลี่ยนแปลง |

## โครงสร้างโฟลเดอร์หลัก

```
C:\zServer\
├── bin\ZServerManager.exe   ← โปรแกรมหลัก
├── apache\                  ← Apache (ติดตั้งผ่านโปรแกรม)
├── php\                     ← PHP
├── mariadb\                 ← MariaDB
├── www\                     ← เว็บของคุณ (DocumentRoot) — วางไฟล์เว็บที่นี่
├── data\                    ← ข้อมูลฐานข้อมูล MariaDB
├── logs\                    ← log ทุกตัวรวมกันที่นี่
├── downloads\               ← ไฟล์ zip ที่ดาวน์โหลดมา (ลบได้เมื่อติดตั้งเสร็จ)
├── backups\                 ← สำรอง config/โฟลเดอร์เก่าอัตโนมัติ
├── src\                     ← ซอร์สโค้ด (C# .NET 10)
├── tools\
│   ├── ZServer.Installer\   ← ติดตั้งผ่าน CLI (สำหรับ scripting)
│   └── ZServer.SelfTest\    ← โปรแกรมทดสอบตัวเอง
├── docs\                    ← เอกสารทั้งหมด
└── zserver.json             ← ค่าตั้งของโปรแกรม
```

## สร้างโปรแกรมจากซอร์ส (สำหรับผู้พัฒนา)

```bash
# build ทั้งหมด
dotnet build src/ZServer.Manager/ZServer.Manager.csproj

# publish ไฟล์เดียว self-contained
dotnet publish src/ZServer.Manager/ZServer.Manager.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -o bin

# ทดสอบตัวเอง (ไม่ต้อง admin)
dotnet run --project tools/ZServer.SelfTest -- --net
```

ดูรายละเอียดสถาปัตยกรรมได้ที่ [docs/ARCHITECTURE_TH.md](docs/ARCHITECTURE_TH.md)

## ข้อควรรู้

- ค่าเริ่มต้นพอร์ต: **80 (HTTP) / 3306 (MariaDB)** — ถ้าเครื่องลง XAMPP ไว้และกำลังรันอยู่ โปรแกรมจะเตือนและบอกวิธีแก้ทันที (หยุด XAMPP หรือเปลี่ยนพอร์ตในหน้าตั้งค่า)
- PHP ต้องเป็นรุ่น **Thread Safe** จึงจะโหลดเป็นโมดูลของ Apache ได้ — โปรแกรมเลือกรุ่นนี้ให้เองแล้ว
- โปรแกรมไม่แตะต้องการตั้งค่าของ XAMPP เดิมในเครื่องแต่อย่างใด
