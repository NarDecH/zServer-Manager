# อ้างอิงไฟล์ Config ทั้งหมด

เอกสารนี้รวมทุกไฟล์ config ที่โปรแกรมจัดการ ที่อยู่ ความหมายของค่า และลำดับความสำคัญเมื่อแก้ไข

---

## 1. `C:\zServer\zserver.json` — ค่าตั้งของโปรแกรม

หัวใจของโปรแกรม — ทุกค่าในหน้า "ตั้งค่า" ถูกเก็บที่นี่ แก้ผ่าน GUI เป็นหลัก แต่แก้ไฟล์ตรง ๆ ได้ (รูปแบบ JSON)

```json
{
  "General": {
    "MinimizeToTray": true,       // ย่อหน้าต่างไป tray หรือไม่
    "AutoStartServices": false    // เริ่มเซอร์วิสอัตโนมัติเมื่อเปิดโปรแกรม
  },
  "Apache": {
    "Port": 80,                    // พอร์ต HTTP (Listen)
    "ServerName": "localhost",
    "ServerAdmin": "admin@localhost",
    "DocumentRoot": "C:\\zServer\\www",
    "KeepAlive": true,
    "KeepAliveTimeout": 5,
    "ThreadsPerChild": 250,
    "EnableRewrite": true,         // โหลด mod_rewrite
    "EnableDeflate": true,         // โหลด mod_deflate + บีบอัด text/css/js/json
    "EnableExpires": true,         // โหลด mod_expires + cache header
    "EnableAutoIndex": true        // แสดงรายการไฟล์เมื่อไม่มี index
  },
  "Php": {
    "MemoryLimit": "256M",
    "UploadMaxFilesize": "64M",
    "PostMaxSize": "64M",
    "MaxExecutionTime": 120,
    "MaxInputTime": 60,
    "MaxInputVars": 3000,
    "DisplayErrors": false,
    "LogErrors": true,
    "Timezone": "Asia/Bangkok",
    "OpCache": true,
    "Extensions": {
      "mysqli": true,
      "pdo_mysql": true,
      "mbstring": true,
      "curl": true,
      "gd": true,
      "openssl": true,
      "zip": true,
      "intl": true,
      "fileinfo": true,
      "exif": false,
      "soap": false,
      "xsl": false
    }
  },
  "MariaDb": {
    "Port": 3306,
    "DataDir": "C:\\zServer\\data",
    "CharacterSet": "utf8mb4",
    "Collation": "utf8mb4_unicode_ci",
    "InnoDBBufferPoolSize": "256M",
    "KeyBufferSize": "64M",
    "MaxConnections": 100,
    "LowerCaseTableNames": 1
  },
  "Services": {
    "Mode": "service",             // "service" หรือ "process"
    "ApacheName": "ZServerApache",
    "MariaDbName": "ZServerMariaDB",
    "StartupType": "auto"          // "auto" หรือ "demand"
  },
  "ApacheInstalled":  { "Version": "2.4.71", "InstalledAt": "...", "SourceUrl": "..." },
  "PhpInstalled":     { "Version": "8.5.13", "InstalledAt": "...", "SourceUrl": "..." },
  "MariaDbInstalled": { "Version": "11.4.10", "InstalledAt": "...", "SourceUrl": "..." },
  "UpdatedAt": "..."
}
```

หมายเหตุ:
- `Installed*` — โปรแกรมเขียนให้อัตโนมัติหลังติดตั้ง อย่าแก้เอง
- `DataDir` — ตั้งครั้งแรกตอนติดตั้ง MariaDB; เปลี่ยนทีหลังต้องย้ายไฟล์ข้อมูลด้วย
- แก้ผ่านหน้า "แก้ไขไฟล์ Config" ได้ (มีตรวจ JSON ก่อนบันทึก)

---

## 2. `apache\conf\httpd.conf` — config ของ Apache

**โปรแกรมสร้างให้ใหม่ทั้งไฟล์** ทุกครั้งที่ติดตั้ง Apache/PHP หรือกดบันทึกหน้าตั้งค่า (สำรองเดิมก่อนเสมอ) — โครงสร้างที่สร้าง:

```
Define SRVROOT "C:/zServer/apache"      ← รากของ Apache (forward slash)
ServerRoot "${SRVROOT}"
Listen 80                               ← จาก Apache.Port
ServerName localhost:80
ServerAdmin admin@localhost

# Modules — โปรแกรมใส่เฉพาะโมดูลที่มีไฟล์ .so จริงใน apache\modules\
# (build ใหม่ของ ApacheLounge คอมไพล์ mod_mpm_winnt รวมใน httpd.exe แล้ว
#  จึงไม่ต้องและห้าม LoadModule mpm_winnt — โปรแกรมจัดการให้เอง)
LoadModule authz_core_module modules/mod_authz_core.so
...                                     + mod_rewrite/deflate/expires/filter ตาม Enable*
LoadModule php_module "C:/zServer/php/php8apache2_4.dll"   ← ใส่เฉพาะเมื่อ PHP (TS) ติดตั้งแล้ว
PHPIniDir "C:/zServer/php"
AddHandler application/x-httpd-php .php

DocumentRoot "C:/zServer/www"           ← จาก Apache.DocumentRoot
<Directory "C:/zServer/www">            AllowOverride All, Require all granted </Directory>
DirectoryIndex index.php index.html index.htm

ErrorLog "C:/zServer/logs/apache-error.log"
CustomLog "C:/zServer/logs/apache-access.log" combined

IncludeOptional conf/zserver-*.conf     ← จุดต่อขยายของผู้ใช้ (ด้านล่าง)
```

**จุดต่อขยายที่ไม่ถูกเขียนทับ**: สร้างไฟล์ `apache\conf\zserver-<อะไรก็ได้>.conf` เช่นตั้ง virtual host:

```apache
# apache\conf\zserver-vhosts.conf — โปรแกรมไม่แตะไฟล์ที่ขึ้นต้น zserver-
<VirtualHost *:80>
    ServerName myproject.local
    DocumentRoot "C:\zServer\www\myproject"
    <Directory "C:\zServer\www\myproject">
        Require all granted
    </Directory>
</VirtualHost>
```

หลังแก้ไฟล์เอง เปิด zServer Manager → หน้า "แก้ไขไฟล์ Config" → บันทึก โปรแกรมจะรัน `httpd -t` ตรวจให้ทันที

---

## 3. `php\php.ini` — config ของ PHP

วิธีสร้าง: เอา `php.ini-development` (มากับ PHP zip) เป็นฐาน แล้วแทรก **managed block** ตรงท้ายไฟล์:

```ini
; ===== zServer managed settings — BEGIN =====
; ส่วนนี้โปรแกรม zServer Manager จัดการ — จะถูกเขียนใหม่เมื่อกดบันทึกในหน้า "ตั้งค่า"
extension_dir = "C:\zServer\php\ext"
memory_limit = "256M"
upload_max_filesize = "64M"
post_max_size = "64M"
max_execution_time = 120
max_input_time = 60
max_input_vars = 3000
display_errors = Off
log_errors = On
error_log = "C:/zServer/logs/php-error.log"
date.timezone = "Asia/Bangkok"
extension=mysqli
extension=mbstring
...
zend_extension = "C:\zServer\php\ext\php_opcache.dll"
opcache.enable = 1
; ===== zServer managed settings — END =====
```

กฎ:
- ค่าใน managed block **ชนะ** ค่าที่อยู่ก่อนหน้าในไฟล์ (PHP ใช้ค่าที่ประกาศทีหลัง)
- แก้ค่าอื่นนอก block ได้อิสระ — โปรแกรมไม่แตะ
- `extension=ชื่อ` จะใส่เฉพาะตัวที่มีไฟล์ `php_ชื่อ.dll` จริงใน `php\ext\` จึงไม่ทำ PHP พังจาก extension หาย
- ลบ managed block = ค่าทั้งหมดกลับไปตาม php.ini-development

---

## 4. `mariadb\my.ini` — config ของ MariaDB

โปรแกรมสร้างใหม่ทั้งไฟล์ (สำรองเดิมก่อน):

```ini
[mysqld]
basedir = "C:/zServer/mariadb"
datadir = "C:/zServer/data"
port = 3306
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
lower_case_table_names = 1
innodb_buffer_pool_size = 256M
key_buffer_size = 64M
max_connections = 100
max_allowed_packet = 64M
log_error = "C:/zServer/logs/mariadb-error.log"
log_timestamps = SYSTEM
skip-name-resolve

[client]
port = 3306
default-character-set = utf8mb4

[mysql]
default-character-set = utf8mb4
```

หมายเหตุ:
- เซอร์วิส `ZServerMariaDB` ถูกลงทะเบียนให้ชี้ `--defaults-file="C:\zServer\mariadb\my.ini"` จึงอ่านไฟล์นี้เสมอ
- `lower_case_table_names` — ห้ามเปลี่ยนหลังมีข้อมูลแล้ว (ตารางจะอ่านไม่ได้)
- ต้องรีสตาร์ท MariaDB จึงจะมีผลทุกครั้ง

---

## 5. ไฟล์อื่นที่ควรรู้จัก

| ไฟล์ | ผู้สร้าง | แก้ได้ไหม |
|---|---|---|
| `www\index.php`, `www\phpinfo.php` | โปรแกรม (หน้าทดสอบ) | ลบ/แก้ได้เลย เมื่อเริ่มโปรเจกต์จริง |
| `logs\*.log` | Apache/PHP/MariaDB/โปรแกรม | ลบ/ล้างได้ (มีปุ่มในหน้าดู Logs) |
| `logs\apache.pid`, `logs\mariadb.pid` | โปรแกรม (โหมด process เท่านั้น) | อย่าแก้ — ลบได้เมื่อไม่มีเซอร์วิสรัน |
| `backups\configs\*` | โปรแกรม (สำรองก่อนเขียนทับ) | ใช้คืน config เมื่อแก้พัง |
| `backups\apache-old-<เวลา>\` ฯลฯ | โปรแกรม (ก่อนอัพเดท) | ใช้ rollback เวอร์ชัน |
| `downloads\*.zip` | โปรแกรม | ลบได้เมื่อติดตั้งเสร็จ (เก็บไว้ก็ติดตั้งซ้ำได้เร็วขึ้น) |

---

## 6. ลำดับความสำคัญเมื่อค่าซ้ำกัน

```
หน้า "ตั้งค่า" (zserver.json)
   └→ เขียนทับ managed block ใน php.ini + เขียนใหม่ httpd.conf / my.ini
   └→ ส่วนที่แก้เองในไฟล์ config นอกจุดจัดการ = คงอยู่เสมอ
```

สรุป: อยากแก้ค่าถาวรแบบไม่ถูกทับ → ใช้หน้า "ตั้งค่า"; อยากใส่ directive พิเศษ → ใช้ `conf/zserver-*.conf` หรือแก้นอก managed block
