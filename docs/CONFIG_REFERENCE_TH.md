# อ้างอิงไฟล์ Config ทั้งหมด

เอกสารนี้รวมทุกไฟล์ config ที่โปรแกรมจัดการ ที่อยู่ ความหมายของค่า และลำดับความสำคัญเมื่อแก้ไข (ฉบับ v1.5.0)

---

## 1. `C:\zServer\zserver.json` — ค่าตั้งของโปรแกรม

หัวใจของโปรแกรม — ทุกค่าในหน้า "ตั้งค่า" ถูกเก็บที่นี่ แก้ผ่าน GUI เป็นหลัก แต่แก้ไฟล์ตรง ๆ ได้ (รูปแบบ JSON)

```json
{
  "General": {
    "MinimizeToTray": true,        // ย่อ/ปิดหน้าต่าง → ซ่อนไว้ใน tray
    "AutoStartServices": false,    // เริ่มเซอร์วิสอัตโนมัติเมื่อเปิดโปรแกรม
    "StartWithWindows": false,     // เปิดโปรแกรมอัตโนมัติเมื่อเข้าสู่ระบบ (Task Scheduler)
    "DarkTheme": true,             // ธีมมืด (false = สว่าง)
    "WatchdogAutoRestart": false,  // เซอร์วิสล้ม → เริ่มให้ใหม่อัตโนมัติ + แจ้งเตือน
    "LogRetentionDays": 14,        // เก็บ log ไว้กี่วันก่อนบีบอัดเก็บ (0 = ไม่จำกัด)
    "LastUpdateCheck": "2026-11-20T02:18:21+07:00"  // ตรวจเวอร์ชันล่าสุดเมื่อไร (กันตรวจถี่เกิน)
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
    "EnableDeflate": true,         // โหลด mod_deflate + mod_filter + บีบอัด text/css/js/json
    "EnableExpires": true,         // โหลด mod_expires + cache header
    "EnableAutoIndex": true,       // แสดงรายการไฟล์เมื่อไม่มี index
    "EnableHttps": false,          // เปิด HTTPS (ต้องมีใบรับรองใน conf\ssl\ ก่อน)
    "HttpsPort": 443,              // พอร์ต HTTPS
    "VirtualHosts": [              // เว็บเพิ่มเติม (จัดการผ่านแท็บ Virtual Hosts)
      { "Name": "myproject.local", "Path": "C:\\zServer\\www\\myproject", "Enabled": true }
    ]
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
    "XdebugEnabled": false,        // เปิด Xdebug (ต้องมี php_xdebug.dll ใน ext\)
    "XdebugPort": 9003,            // พอร์ต debug ของ VS Code
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
    "BindAddress": "127.0.0.1",    // ฟัง localhost เท่านั้น ("" = ทุก interface — เสี่ยง)
    "RootPasswordEnc": "",         // รหัสผ่าน root เข้ารหัส DPAPI (ให้งานอัตโนมัติใช้)
    "AutoBackupEnabled": false,    // สำรองฐานข้อมูลอัตโนมัติทุกวัน
    "AutoBackupTime": "03:00",     // เวลาทำสำรอง (HH:mm)
    "AutoBackupRetentionDays": 7,  // เก็บไฟล์สำรองไว้กี่วัน
    "LastDbBackupDate": null,      // สำรองล่าสุดเมื่อไร
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
  "RemoteCatalogUrl": "",          // URL catalog.json เสริม (เว้นว่าง = ปิด)
  "ProgramUpdateUrl": "",          // URL JSON {version,url} แจ้งเวอร์ชันโปรแกรมใหม่ (เว้นว่าง = ปิด)
  "ApacheInstalled":  { "Version": "2.4.71", "InstalledAt": "...", "SourceUrl": "..." },
  "PhpInstalled":     { "Version": "8.5.13", "InstalledAt": "...", "SourceUrl": "..." },
  "MariaDbInstalled": { "Version": "11.8.6", "InstalledAt": "...", "SourceUrl": "..." },
  "UpdatedAt": "..."
}
```

หมายเหตุ:
- `Installed*` — โปรแกรมเขียนให้อัตโนมัติหลังติดตั้ง/สลับ PHP อย่างเดียว อย่าแก้เอง
- `DataDir` — ตั้งครั้งแรกตอนติดตั้ง MariaDB; เปลี่ยนทีหลังต้องย้ายไฟล์ข้อมูลด้วย
- `RootPasswordEnc` — เข้ารหัส DPAPI (ถอดได้เฉพาะ user เดียวกันบนเครื่องเดียวกัน) — ใส่ข้อความเปล่าเองไม่ได้ ตั้งผ่านหน้าตั้งค่า
- `VirtualHosts` — เพิ่ม/ลบผ่านแท็บ Virtual Hosts (โปรแกรมเขียน `zserver-vhosts.conf` + hosts file ให้)
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
                                        + ssl_module + socache_shmcb เมื่อเปิด HTTPS
LoadModule php_module "C:/zServer/php/php8apache2_4.dll"   ← ใส่เฉพาะเมื่อ PHP (TS) ติดตั้งแล้ว
PHPIniDir "C:/zServer/php"
AddHandler application/x-httpd-php .php

DocumentRoot "C:/zServer/www"           ← จาก Apache.DocumentRoot
<Directory "C:/zServer/www">            AllowOverride All, Require all granted </Directory>
DirectoryIndex index.php index.html index.htm

PidFile "C:/zServer/logs/apache.pid"    ← กำหนดชัดเจน (กันหาโฟลเดอร์ไม่เจอ)
ErrorLog "C:/zServer/logs/apache-error.log"
CustomLog "C:/zServer/logs/apache-access.log" combined

# ---------- HTTPS ---------- (เฉพาะเมื่อ EnableHttps + มีใบรับรองจริง)
<IfModule ssl_module>
    Listen 443
    <VirtualHost *:443>                 ← เว็บหลัก
        ServerName localhost:443
        DocumentRoot "C:/zServer/www"
        SSLEngine on
        SSLCertificateFile "C:/zServer/apache/conf/ssl/cert.pem"
        SSLCertificateKeyFile "C:/zServer/apache/conf/ssl/key.pem"
        ...
    </VirtualHost>
    <VirtualHost *:443>                 ← + Virtual Host ทุกตัว (ใบรับรองเดียวกัน)
        ServerName myproject.local:443
        ...
    </VirtualHost>
</IfModule>

IncludeOptional conf/zserver-*.conf     ← จุดต่อขยายของผู้ใช้ (ด้านล่าง)
```

**จุดต่อขยายที่ไม่ถูกเขียนทับ**: สร้างไฟล์ `apache\conf\zserver-<อะไรก็ได้>.conf` เช่นตั้งค่าเพิ่มเติม:

```apache
# apache\conf\zserver-extra.conf — โปรแกรมไม่แตะไฟล์ที่ขึ้นต้น zserver-
ServerTokens Full
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
zend_extension = "C:\zServer\php\ext\php_xdebug.dll"   ← เฉพาะเมื่อเปิด Xdebug + มีไฟล์
xdebug.mode = debug
xdebug.client_port = 9003
xdebug.start_with_request = yes
; ===== zServer managed settings — END =====
```

กฎ:
- ค่าใน managed block **ชนะ** ค่าที่อยู่ก่อนหน้าในไฟล์ (PHP ใช้ค่าที่ประกาศทีหลัง)
- แก้ค่าอื่นนอก block ได้อิสระ — โปรแกรมไม่แตะ
- `extension=ชื่อ` จะใส่เฉพาะตัวที่มีไฟล์ `php_ชื่อ.dll` จริงใน `php\ext\` จึงไม่ทำ PHP พังจาก extension หาย
- ลบ managed block = ค่าทั้งหมดกลับไปตาม php.ini-development

> ระบบ PHP หลายเวอร์ชัน: `php\` เป็น junction ชี้ไป `php-versions\{เวอร์ชัน}\` — php.ini ของแต่ละเวอร์ชันแยกกัน (สลับเวอร์ชันแล้วกดบันทึกหน้าตั้งค่า 1 ครั้งเพื่อเขียน php.ini ของเวอร์ชันใหม่)

---

## 4. `mariadb\my.ini` — config ของ MariaDB

โปรแกรมสร้างใหม่ทั้งไฟล์ (สำรองเดิมก่อน):

```ini
[mysqld]
basedir = "C:/zServer/mariadb"
datadir = "C:/zServer/data"
port = 3306
bind-address = 127.0.0.1        ← ฟัง localhost เท่านั้น (ปลอดภัย — ปิดได้ในหน้าตั้งค่า)
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
lower_case_table_names = 1
innodb_buffer_pool_size = 256M
key_buffer_size = 64M
max_connections = 100
max_allowed_packet = 64M
log_error = "C:/zServer/logs/mariadb-error.log"
skip-name-resolve

[client]
port = 3306
default-character-set = utf8mb4

[mysql]
default-character-set = utf8mb4
```

หมายเหตุ:
- เซอร์วิส `ZServerMariaDB` ถูกลงทะเบียนให้ชี้ `--defaults-file="C:\zServer\mariadb\my.ini"` จึงอ่านไฟล์นี้เสมอ
- `bind-address = 127.0.0.1` ทำให้เครื่องอื่นเชื่อมต่อฐานข้อมูล**ไม่ได้**แม้เปิดพอร์ต Firewall — ปิดได้ในหน้าตั้งค่า (มีเตือนความปลอดภัย)
- `lower_case_table_names` — ห้ามเปลี่ยนหลังมีข้อมูลแล้ว (ตารางจะอ่านไม่ได้)
- ต้องรีสตาร์ท MariaDB จึงจะมีผลทุกครั้ง

---

## 5. `apache\conf\zserver-vhosts.conf` — Virtual Hosts

สร้างโดยแท็บ Virtual Hosts (สำรองเดิมก่อนเขียนทับ):

```apache
# Virtual Hosts — สร้างโดย zServer Manager (จัดการผ่านหน้าตั้งค่า > Virtual Hosts)
# เว็บเพิ่มเติม 2 ตัว — include ผ่าน conf/zserver-*.conf อัตโนมัติ

# --- myproject.local ---
<VirtualHost *:80>
    ServerName myproject.local
    DocumentRoot "C:/zServer/www/myproject"
    <Directory "C:/zServer/www/myproject">
        Options +Indexes +FollowSymLinks
        AllowOverride All
        Require all granted
    </Directory>
</VirtualHost>
```

เว็บเหล่านี้ยังได้ **https://ชื่อเว็บ** อัตโนมัติด้วยเมื่อเปิด HTTPS (ใบรับรองเดียวครอบทุกโดเมน)

---

## 6. ไฟล์อื่นที่ควรรู้จัก

| ไฟล์ | ผู้สร้าง | แก้ได้ไหม |
|---|---|---|
| `www\index.php`, `www\phpinfo.php` | โปรแกรม (หน้าทดสอบ) | ลบ/แก้ได้เลย เมื่อเริ่มโปรเจกต์จริง |
| `www\adminer.php` | ติดตั้งผ่านหน้า "ติดตั้ง" | จัดการฐานข้อมูลผ่านเว็บ |
| `logs\*.log` | Apache/PHP/MariaDB/โปรแกรม | ลบ/ล้างได้ (มีปุ่มในหน้าดู Logs + ล้างไฟล์ log) |
| `logs\apache.pid`, `logs\mariadb.pid` | โปรแกรม (โหมด process เท่านั้น) | อย่าแก้ — ลบได้เมื่อไม่มีเซอร์วิสรัน |
| `apache\conf\ssl\cert.pem` + `key.pem` | mkcert หรือนำเข้าใบรับรองจริง | จัดการผ่านหน้า "HTTPS" เท่านั้น |
| `backups\configs\*` | โปรแกรม (สำรองก่อนเขียนทับ) | ใช้คืน config เมื่อแก้พัง |
| `backups\apache-old-<เวลา>\` ฯลฯ | โปรแกรม (ก่อนอัพเดท) | ใช้ rollback เวอร์ชัน |
| `backups\databases\*.sql` | สำรองฐานข้อมูล (มือ/อัตโนมัติ) | ใช้กู้คืนฐานข้อมูล |
| `backups\log-archives\*.zip` | log rotation | เก็บ log เก่าบีบอัดไว้ |
| `backups\hosts-*.bak` | โปรแกรม (ก่อนแก้ hosts) | ใช้คืน hosts file |
| `downloads\*.zip`, `mkcert.exe` | โปรแกรม | ลบได้เมื่อติดตั้งเสร็จ (เก็บไว้ติดตั้งซ้ำได้เร็วขึ้น) |
| `php-versions\{เวอร์ชัน}\` | ระบบ PHP หลายเวอร์ชัน | จัดการผ่านหน้าติดตั้ง/สลับเวอร์ชัน |

---

## 7. ลำดับความสำคัญเมื่อค่าซ้ำกัน

```
หน้า "ตั้งค่า" (zserver.json)
   └→ เขียนทับ managed block ใน php.ini + เขียนใหม่ httpd.conf / my.ini / zserver-vhosts.conf
   └→ ส่วนที่แก้เองในไฟล์ config นอกจุดจัดการ = คงอยู่เสมอ
```

สรุป: อยากแก้ค่าถาวรแบบไม่ถูกทับ → ใช้หน้า "ตั้งค่า"; อยากใส่ directive พิเศษ → ใช้ `conf/zserver-*.conf` หรือแก้นอก managed block
