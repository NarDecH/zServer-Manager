# แก้ปัญหาที่พบบ่อย (Troubleshooting)

ตรวจพื้นฐานก่อนเสมอ: เปิดหน้า **ดู Logs** ดูไฟล์ที่เกี่ยวข้อง — ข้อความ error จริงแทบทุกกรณีอยู่ใน `logs\apache-error.log` หรือ `logs\mariadb-error.log`

---

## พอร์ตถูกใช้งาน / เริ่มเซอร์วิสไม่ได้เพราะพอร์ตชน

**อาการ**: กด "เริ่ม" แล้วขึ้น "พอร์ต 80 ถูกใช้โดย Apache (น่าจะมาจาก XAMPP)..." หรือสถานะการ์ดขึ้น ⚠

**ตรวจ**: ใช้คำสั่งดูว่าใครใช้พอร์ต
```bat
netstat -ano | findstr ":80 "   (หรือ :3306)
tasklist | findstr <PID ที่เจอ>
```

**วิธีแก้** — เลือกอย่างใดอย่างหนึ่ง:
1. หยุดโปรแกรมเดิมที่ใช้พอร์ต (XAMPP Control Panel → Stop หรือ `sc stop Apache2.4`)
2. เปลี่ยนพอร์ตของ zServer: หน้า **ตั้งค่า** → Apache = 8080 / MariaDB = 3307 → บันทึก → รีสตาร์ทเซอร์วิส
3. ตั้ง XAMPP ไม่ให้ auto-start: `sc config Apache2.4 start= demand`

> โปรแกรมตรวจพอร์ตก่อนเริ่มเซอร์วิสทุกครั้ง ดังนั้นถ้าพอร์ตชนจะไม่เริ่มเองพร้อมกับแจ้งผู้ร้าวชัดเจน

---

## Apache เริ่มไม่ได้

**ตรวจทีละข้อ:**
1. เปิด `logs\apache-error.log` — อ่านบรรทัดท้ายสุด
2. รันทดสอบ config เองได้:
   ```
   C:\zServer\apache\bin\httpd.exe -t -d "C:\zServer\apache" -f "C:\zServer\apache\conf\httpd.conf"
   ```
3. **(HTTP No Listening)** หรือ `(OS 10048) make_sock: could not bind to address` = พอร์ตชน (ดูหัวข้อแรก)
4. ข้อความ `Cannot load .../php8apache2_4.dll` = PHP ตัวที่ติดตั้งไม่ใช่รุ่น Thread Safe → ติดตั้ง PHP ใหม่จากหน้า "ติดตั้ง" (โปรแกรมเลือกรุ่น TS ให้แล้ว)
5. ข้อความ `api-ms-win-crt...dll missing` หรือขึ้น error เกี่ยวกับ VCRUNTIME140.dll = ยังไม่ติดตั้ง VC++ Redistributable → หน้า "ติดตั้ง" → กดปุ่มติดตั้ง VC++ Redistributable → รีสตาร์ทเครื่องหลังติดตั้ง
6. แก้ config เองแล้วพัง: เอาไฟล์สำรองใน `backups\configs\httpd.conf.*.bak` กลับมาทับ แล้วรีสตาร์ท

---

## MariaDB เริ่มไม่ได้

1. เปิด `logs\mariadb-error.log` — บรรทัดท้ายสุดมักบอกสาเหตุตรง ๆ
2. `Can't open .../my.ini` = ไฟล์ my.ini หาย → เปิดหน้า ตั้งค่า → กดบันทึก (โปรแกรมสร้างให้ใหม่) หรือติดตั้ง MariaDB ใหม่
3. `[ERROR] ... InnoDB: ... ibdata1` = ไฟล์ข้อมูลพัง/หาย — ถ้ายังไม่มีข้อมูลสำคัญ ลบโฟลเดอร์ `data\` แล้วรัน `mariadb\bin\mariadb-install-db.exe --datadir="C:\zServer\data"` ใหม่; ถ้ามีข้อมูลสำคัญ **ห้ามลบ** — ติดตั้ง MariaDB เวอร์ชันเดิมจาก `backups\mariadb-old-...` กลับ
4. เริ่มผ่าน service ไม่ได้แต่เริ่มโหมด process ได้ = เซอร์วิสลงทะเบียน path ผิด (เช่นหลังย้ายโฟลเดอร์) → ตั้งค่า → เซอร์วิส → ถอนเซอร์วิส MariaDB แล้วกดติดตั้งใหม่

---

## เว็บเปิดได้แต่หน้า PHP ขึ้นดาวน์โหลดไฟล์ / ขึ้น source code

= Apache ยังไม่โหลดโมดูล PHP ตรวจว่า `httpd.conf` มีบรรทัดเหล่านี้:
```
LoadModule php_module "C:/zServer/php/php8apache2_4.dll"
PHPIniDir "C:/zServer/php"
AddHandler application/x-httpd-php .php
```
ถ้าไม่มี = PHP ยังไม่ได้ติดตั้งหรือไม่ใช่รุ่น TS — ติดตั้ง PHP จากหน้า "ติดตั้ง" แล้วโปรแกรมจะใส่ให้เอง

---

## PHP เว็บขึ้น 500 / fatal error เรื่อง extension

เช่น `Uncaught Error: Call to undefined function mysqli_connect()` = extension ที่ต้องใช้ยังปิดอยู่ → หน้า ตั้งค่า → PHP → เปิด extension ที่ต้องการ (mysqli, mbstring ฯลฯ) → บันทึก → รีสตาร์ท Apache

---

## เซอร์วิสติดตั้งไม่ได้ / ขึ้น "Access denied"

- โปรแกรมต้องรันแบบ Administrator — ดับเบิลคลิก exe แล้วกด "ใช่" ที่ UAC ทุกครั้ง
- ถ้าชื่อเซอร์วิสซ้ำกับของเดิม (เช่นเคยลงแล้ว) → ตั้งค่า → เซอร์วิส → ถอนเซอร์วิสก่อน แล้วติดตั้งใหม่
- เซอร์วิสค้าง "Starting..." → `sc query ZServerApache` ดูสถานะ, ลอง `sc stop` + เริ่มใหม่, ถ้ายังค้าง `sc delete <ชื่อ>` แล้วติดตั้งใหม่จากหน้าตั้งค่า

---

## หน้าเว็บขึ้น 403 Forbidden

= Apache เห็นโฟลเดอร์แต่ไม่มีสิทธิ์เข้า สาเหตุที่พบบ่อย:
1. DocumentRoot ชี้โฟลเดอร์ที่ไม่มีอยู่ → ตรวจว่าโฟลเดอร์มีจริง (ตั้งค่า → Apache → DocumentRoot)
2. มีไฟล์ index หรือไม่ — ถ้าไม่มีและปิด directory listing จะ 403
3. แก้ DocumentRoot ไปที่อื่นแล้วไม่มี `<Directory>` อนุญาต → ใช้ค่าผ่านหน้าตั้งค่า (โปรแกรมใส่ Require all granted ให้เอง)

---

## อัพโหลดไฟล์ใหญ่ไม่ผ่าน

เพิ่ม 3 ค่าในหน้า ตั้งค่า → PHP: `upload_max_filesize`, `post_max_size` (และถ้าใช้ nginx หน้าบ้านไม่เกี่ยว ที่นี่ใช้ Apache) แล้วรีสตาร์ท Apache ขีดจำกัดในโค้ดของเว็บ (เช่น WordPress) ต้องเพิ่มด้วย

---

## เปลี่ยนพอร์ตแล้วยังเข้าเว็บพอร์ตเดิมได้

= เซอร์วิสยังรันด้วย config เก่า → รีสตาร์ท Apache (หน้าหลัก → ปุ่มรีสตาร์ท) แล้วกดบันทึกหน้าตั้งค่าอีกครั้งถ้ายังไม่ถูก

---

## เชื่อมต่อฐานข้อมูลจากเว็บไม่ได้ (connection refused)

1. การ์ด MariaDB ต้องเป็น ● กำลังทำงาน
2. โค้ด PHP ต้องใช้ `host=127.0.0.1` (หรือ localhost), `port=3306`, `user=root`, `password=` ตามที่ตั้งไว้
   ```php
   $db = mysqli_connect("127.0.0.1", "root", "", "mydb", 3306);
   ```
3. ถ้าตั้งรหัสผ่าน root แล้วลืม: แก้ผ่าน `mariadb\bin\mariadb.exe --port=3306 -u root -p` หรือรีเซ็ตด้วยการลบ datadir (สูญข้อมูล!) จึงควรจดรหัสผ่านไว้

---

## โปรแกรมเปิดไม่ขึ้นเลย

1. ถ้าไม่เด้ง UAC เลย = ไฟล์ถูก block หลัง copy จากที่อื่น → คลิกขวาไฟล์ exe → Properties → ติ๊ก "Unblock" → OK
2. ตรวจว่าเปิดจากที่ที่มี `zserver.json` หรืออยู่ในโครงสร้าง `C:\zServer\bin\` ถูกต้อง (โปรแกรมหาโฟลเดอร์หลักอัตโนมัติจากพาธตัวเอง หรือตั้งผ่าน env `ZSERVER_HOME`)
3. ถ้าเปิดได้แต่ฟ้องอ่านไฟล์ไม่ได้ = โฟลเดอร์โดนกันสิทธิ์ → รัน cmd เป็น admin: `icacls C:\zServer /grant Users:(OI)(CI)F /T`

---

## ดาวน์โหลดไม่สำเร็จ / ตรวจ SHA256 ไม่ผ่าน

- อินเทอร์เน็ตหลุดกลางทาง → ลองใหม่ (ไฟล์เก่า .part ถูกลบให้อัตโนมัติ)
- SHA256 ไม่ผ่าน = ไฟล์ที่โหลดเสียหายหรือถูกแก้ไข โปรแกรมจะทิ้งไฟล์นั้นให้เองและไม่ติดตั้ง — ลองใหม่ ถ้าซ้ำลบไฟล์ zip เก่าใน `downloads\` แล้วลองอีกครั้ง
- เว็บปลายทางเปลี่ยน URL → อัพเดทโปรแกรมเวอร์ชันใหม่ (แคตตาล็อกอยู่ใน `src\ZServer.Core\VersionCatalog.cs`)

---

## ระบบกลับคืน (Rollback) หลังอัพเดทแล้วมีปัญหา

1. หยุดเซอร์วิสที่เกี่ยวข้อง
2. ลบโฟลเดอร์ใหม่ (`apache\` / `php\` / `mariadb\`)
3. ย้ายโฟลเดอร์สำรองจาก `backups\<ชื่อ>-old-<วันเวลา>\` กลับมาใช้ชื่อเดิม (`apache\` เป็นต้น)
4. config เดิมอยู่ใน `backups\configs\` — กลับไฟล์ที่ต้องการ
5. เริ่มเซอร์วิส / หรือเปิดโปรแกรมแล้วกดเริ่ม

---

## ตรวจสุขภาพระบบทั้งหมดในหนึ่งคำสั่ง

```bat
:: Apache config
C:\zServer\apache\bin\httpd.exe -t -d "C:\zServer\apache" -f "C:\zServer\apache\conf\httpd.conf"

:: PHP
C:\zServer\php\php.exe -v
C:\zServer\php\php.exe -m   (รายการ extension ที่โหลดได้)

:: MariaDB
C:\zServer\mariadb\bin\mariadb.exe --port=3306 -u root -p -e "SELECT VERSION();"

:: เซอร์วิส
sc query ZServerApache
sc query ZServerMariaDB

:: พอร์ต
netstat -ano | findstr ":80 :3306"
```
