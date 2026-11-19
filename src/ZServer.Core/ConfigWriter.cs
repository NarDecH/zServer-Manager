using System.Text;
using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// สร้างไฟล์ config ของ Apache / PHP / MariaDB จากค่าที่ตั้งไว้ใน zserver.json
/// — สำรองไฟล์เดิมลง backups\ ทุกครั้งก่อนเขียนทับ
/// </summary>
public sealed class ConfigWriter
{
    private readonly ServerPaths _p;
    private readonly AppConfig _c;
    private readonly Logger _log;

    public ConfigWriter(ServerPaths paths, AppConfig config, Logger log)
    {
        _p = paths;
        _c = config;
        _log = log;
    }

    private static string Fw(string path) => path.Replace('\\', '/'); // forward slashes สำหรับ config

    // ================= สำรองไฟล์ =================
    public string BackupConfig(string file)
    {
        if (!File.Exists(file)) return "";
        var backupDir = Path.Combine(_p.BackupsDir, "configs");
        Directory.CreateDirectory(backupDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dest = Path.Combine(backupDir, Path.GetFileName(file) + "." + stamp + ".bak");
        File.Copy(file, dest, overwrite: true);
        _log.Info($"สำรองไฟล์เดิม: {file} → {dest}");
        return dest;
    }

    private static void WriteFile(string path, string content, Encoding? enc = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Windows ใช้ CRLF — ไฟล์ config ที่ไม่มี CRLF เปิดใน Notepad/TextBox จะติดกันเป็นบรรทัดเดียว
        content = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(path, content, enc ?? new UTF8Encoding(false));
    }

    // ================= Apache: httpd.conf =================
    public bool PhpModuleAvailable => File.Exists(_p.PhpModuleDll);

    public void WriteApacheConf()
    {
        BackupConfig(_p.ApacheConf);
        var a = _c.Apache;
        var docRoot = Fw(string.IsNullOrWhiteSpace(a.DocumentRoot) ? _p.WwwDir : a.DocumentRoot);
        var phpBlock = PhpModuleAvailable
            ? $"""
              # ===== PHP (โหลดเป็นโมดูลของ Apache) =====
              LoadModule php_module "{Fw(_p.PhpModuleDll)}"
              PHPIniDir "{Fw(_p.PhpDir)}"
              AddHandler application/x-httpd-php .php
              """
            : "# ===== PHP =====\n# ยังไม่ได้ติดตั้ง PHP — ติดตั้งผ่านแท็บ \"ติดตั้ง\" แล้ว config จะเชื่อมกับ Apache ให้อัตโนมัติ";

        // ---- HTTPS (ต้องมีใบรับรองจาก mkcert ใน conf\ssl\ ก่อนจึงจะเปิดได้) ----
        var certFile = Path.Combine(_p.ApacheDir, "conf", "ssl", "cert.pem");
        var keyFile = Path.Combine(_p.ApacheDir, "conf", "ssl", "key.pem");
        var httpsReady = a.EnableHttps && File.Exists(certFile);
        var httpsListen = httpsReady ? "\nListen " + a.HttpsPort : "";
        var httpsBlock = httpsReady
            ? $"""
              # ---------- HTTPS (ใบรับรองทดสอบจาก mkcert) ----------
              <IfModule ssl_module>
                  Listen {a.HttpsPort}
                  <VirtualHost *:{a.HttpsPort}>
                      ServerName {a.ServerName}:{a.HttpsPort}
                      DocumentRoot "{docRoot}"
                      SSLEngine on
                      SSLCertificateFile "{Fw(certFile)}"
                      SSLCertificateKeyFile "{Fw(keyFile)}"
                      <Directory "{docRoot}">
                          Options {(a.EnableAutoIndex ? "+Indexes" : "-Indexes")} +FollowSymLinks
                          AllowOverride All
                          Require all granted
                      </Directory>
                  </VirtualHost>
              </IfModule>
              """
            : "# ---------- HTTPS ----------\n# (ยังไม่เปิดใช้ — สร้างใบรับรองและเปิดในหน้าตั้งค่า)";

        var deflateBlock = a.EnableDeflate
            ? $"""
              # ---------- บีบอัดข้อมูล (deflate) ----------
              <IfModule deflate_module>
                  AddOutputFilterByType DEFLATE text/html text/plain text/xml text/css text/javascript
                  AddOutputFilterByType DEFLATE application/javascript application/json application/xml
              </IfModule>
              """
            : "# (ปิดใช้งาน deflate)";

        var expiresBlock = a.EnableExpires
            ? $"""
              # ---------- Cache ฝั่ง browser (expires) ----------
              <IfModule expires_module>
                  ExpiresActive On
                  ExpiresByType image/jpeg "access plus 1 month"
                  ExpiresByType image/png "access plus 1 month"
                  ExpiresByType image/gif "access plus 1 month"
                  ExpiresByType image/webp "access plus 1 month"
                  ExpiresByType text/css "access plus 1 week"
                  ExpiresByType application/javascript "access plus 1 week"
                  ExpiresByType text/html "access plus 0 seconds"
              </IfModule>
              """
            : "# (ปิดใช้งาน expires)";

        var moduleList = new List<string>
        {
            "mpm_winnt_module modules/mod_mpm_winnt.so",
            "authn_file_module modules/mod_authn_file.so",
            "authn_core_module modules/mod_authn_core.so",
            "authz_host_module modules/mod_authz_host.so",
            "authz_groupfile_module modules/mod_authz_groupfile.so",
            "authz_user_module modules/mod_authz_user.so",
            "authz_core_module modules/mod_authz_core.so",
            "access_compat_module modules/mod_access_compat.so",
            "auth_basic_module modules/mod_auth_basic.so",
            "log_config_module modules/mod_log_config.so",
            "filter_module modules/mod_filter.so",
            "env_module modules/mod_env.so",
            "setenvif_module modules/mod_setenvif.so",
            "headers_module modules/mod_headers.so",
            "mime_module modules/mod_mime.so",
            "dir_module modules/mod_dir.so",
            "alias_module modules/mod_alias.so",
            "autoindex_module modules/mod_autoindex.so",
            "negotiation_module modules/mod_negotiation.so",
            "include_module modules/mod_include.so",
            "actions_module modules/mod_actions.so",
            "status_module modules/mod_status.so",
            "version_module modules/mod_version.so",
        };
        if (a.EnableRewrite) moduleList.Add("rewrite_module modules/mod_rewrite.so");
        if (a.EnableDeflate) moduleList.Add("deflate_module modules/mod_deflate.so");
        if (a.EnableExpires) moduleList.Add("expires_module modules/mod_expires.so");
        if (httpsReady)
        {
            moduleList.Add("ssl_module modules/mod_ssl.so");
            moduleList.Add("socache_shmcb_module modules/mod_socache_shmcb.so");
        }

        // โหลดเฉพาะโมดูลที่มีไฟล์ .so จริง — build ใหม่ของ Apache คอมไพล์บางโมดูล
        // (เช่น mpm_winnt) รวมใน httpd.exe แล้ว จึงห้าม LoadModule ไฟล์ที่ไม่มี
        var modulesDir = Path.Combine(_p.ApacheDir, "modules");
        var canFilter = Directory.Exists(modulesDir);
        var moduleLines = new List<string>();
        foreach (var m in moduleList)
        {
            if (canFilter)
            {
                var parts = m.Split(' ');
                var relPath = parts[1].Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(Path.Combine(_p.ApacheDir, relPath)))
                {
                    _log.Warn($"ข้าม LoadModule {parts[0]} — ไม่พบ {relPath} (โมดูลนี้อาจถูกคอมไพล์รวมใน httpd.exe แล้ว)");
                    continue;
                }
            }
            moduleLines.Add($"LoadModule {m}");
        }
        var modules = string.Join("\n", moduleLines);

        var conf = $$"""
            # ============================================================
            # ไฟล์ config หลักของ Apache — สร้างโดย zServer Manager
            # (แก้ไขไฟล์นี้ได้ แต่ถ้าใช้หน้า "ตั้งค่า" ของโปรแกรม ส่วนที่จัดการจะถูกเขียนใหม่)
            # สร้างเมื่อ: {{DateTime.Now:yyyy-MM-dd HH:mm:ss}}
            # ============================================================
            Define SRVROOT "{{Fw(_p.ApacheDir)}}"

            ServerRoot "${SRVROOT}"
            Listen {{a.Port}}{{httpsListen}}
            ServerName {{a.ServerName}}:{{a.Port}}
            ServerAdmin {{a.ServerAdmin}}

            # ---------- Modules ----------
            {{modules}}

            ThreadsPerChild {{a.ThreadsPerChild}}
            MaxRequestsPerChild 0

            # ---------- ค่าพื้นฐาน ----------
            KeepAlive {{(a.KeepAlive ? "On" : "Off")}}
            MaxKeepAliveRequests 100
            KeepAliveTimeout {{a.KeepAliveTimeout}}
            Timeout 300
            ServerTokens Prod
            ServerSignature Off
            HostnameLookups Off
            TraceEnable Off
            Protocols http/1.1

            TypesConfig conf/mime.types
            AddDefaultCharset UTF-8

            # ---------- DocumentRoot ----------
            DocumentRoot "{{docRoot}}"
            <Directory "/">
                AllowOverride None
                Require all denied
            </Directory>

            <Directory "{{docRoot}}">
                Options {{(a.EnableAutoIndex ? "+Indexes" : "-Indexes")}} +FollowSymLinks -ExecCGI
                AllowOverride All
                Require all granted
            </Directory>

            <IfModule dir_module>
                DirectoryIndex index.php index.html index.htm
            </IfModule>

            <Files ".ht*">
                Require all denied
            </Files>

            # ---------- Logs ----------
            PidFile "{{Fw(_p.ApachePidFile)}}"
            ErrorLog "{{Fw(_p.ApacheErrorLog)}}"
            LogLevel warn
            LogFormat "%h %l %u %t \"%r\" %>s %b \"%{Referer}i\" \"%{User-Agent}i\"" combined
            CustomLog "{{Fw(_p.ApacheAccessLog)}}" combined

            # ---------- PHP ----------
            {{phpBlock}}

            # ---------- MIME ----------
            <IfModule mime_module>
                AddType application/x-compress .Z
                AddType application/x-gzip .gz .tgz
                AddType text/html .shtml
                AddOutputFilter INCLUDES .shtml
            </IfModule>

            {{deflateBlock}}

            {{expiresBlock}}

            {{httpsBlock}}

            # ---------- จุดต่อขยายของผู้ใช้ ----------
            # สร้างไฟล์ conf/zserver-ชื่ออะไรก็ได้.conf แล้วจะถูก include อัตโนมัติ
            IncludeOptional conf/zserver-*.conf
            """;

        WriteFile(_p.ApacheConf, conf);
        _log.Info($"เขียน {Path.GetFileName(_p.ApacheConf)} เรียบร้อย (พอร์ต {a.Port})");
        WriteVhostsConf();
    }

    // ================= Virtual Hosts: conf/zserver-vhosts.conf =================
    public void WriteVhostsConf()
    {
        var file = Path.Combine(_p.ApacheDir, "conf", "zserver-vhosts.conf");
        var enabled = _c.Apache.VirtualHosts
            .Where(v => v.Enabled && !string.IsNullOrWhiteSpace(v.Name) && !string.IsNullOrWhiteSpace(v.Path))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# ============================================================");
        sb.AppendLine("# Virtual Hosts — สร้างโดย zServer Manager (จัดการผ่านหน้าตั้งค่า > Virtual Hosts)");
        sb.AppendLine($"# เว็บเพิ่มเติม {enabled.Count} ตัว — include ผ่าน conf/zserver-*.conf อัตโนมัติ");
        sb.AppendLine();
        foreach (var v in enabled)
        {
            var root = Fw(v.Path);
            sb.AppendLine($"# --- {v.Name} ---");
            sb.AppendLine($"<VirtualHost *:{_c.Apache.Port}>");
            sb.AppendLine($"    ServerName {v.Name}");
            sb.AppendLine($"    DocumentRoot \"{root}\"");
            sb.AppendLine($"    <Directory \"{root}\">");
            sb.AppendLine("        Options +Indexes +FollowSymLinks");
            sb.AppendLine("        AllowOverride All");
            sb.AppendLine("        Require all granted");
            sb.AppendLine("    </Directory>");
            sb.AppendLine("</VirtualHost>");
            sb.AppendLine();
        }
        if (enabled.Count == 0)
            sb.AppendLine("# (ยังไม่มีเว็บเพิ่มเติม)");

        BackupConfig(file);
        WriteFile(file, sb.ToString());
        _log.Info($"เขียน {Path.GetFileName(file)} เรียบร้อย ({enabled.Count} เว็บ)");
    }

    // ================= PHP: php.ini =================
    public void WritePhpIni()
    {
        var php = _c.Php;

        // ถ้ายังไม่มี php.ini ให้ใช้ php.ini-development เป็นฐาน
        if (!File.Exists(_p.PhpIni))
        {
            var devIni = Path.Combine(_p.PhpDir, "php.ini-development");
            var prodIni = Path.Combine(_p.PhpDir, "php.ini-production");
            var baseIni = File.Exists(devIni) ? devIni : (File.Exists(prodIni) ? prodIni : null);
            if (baseIni != null)
            {
                File.Copy(baseIni, _p.PhpIni);
                _log.Info($"สร้าง php.ini จาก {Path.GetFileName(baseIni)} เป็นฐาน");
            }
        }
        BackupConfig(_p.PhpIni);

        var content = File.Exists(_p.PhpIni) ? File.ReadAllText(_p.PhpIni) : "; php.ini (สร้างโดย zServer Manager)\n";

        // ตัด managed block เดิมออกก่อน
        var begin = ServerPaths.PhpIniBeginMarker;
        var end = ServerPaths.PhpIniEndMarker;
        var pattern = Regex.Escape(begin) + @"[\s\S]*?" + Regex.Escape(end) + @"\s*";
        content = Regex.Replace(content, pattern, "", RegexOptions.CultureInvariant);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(begin);
        sb.AppendLine("; ส่วนนี้โปรแกรม zServer Manager จัดการ — จะถูกเขียนใหม่เมื่อกดบันทึกในหน้า \"ตั้งค่า\"");
        sb.AppendLine($$"""
            extension_dir = "{{_p.PhpDir}}\ext"
            memory_limit = "{{php.MemoryLimit}}"
            upload_max_filesize = "{{php.UploadMaxFilesize}}"
            post_max_size = "{{php.PostMaxSize}}"
            max_execution_time = {{php.MaxExecutionTime}}
            max_input_time = {{php.MaxInputTime}}
            max_input_vars = {{php.MaxInputVars}}
            display_errors = {{(php.DisplayErrors ? "On" : "Off")}}
            log_errors = {{(php.LogErrors ? "On" : "Off")}}
            error_log = "{{Fw(_p.PhpErrorLog)}}"
            date.timezone = "{{php.Timezone}}"
            """);

        foreach (var (name, enabled) in php.Extensions)
        {
            if (!enabled) continue;
            var dll = Path.Combine(_p.PhpDir, "ext", "php_" + name + ".dll");
            if (!File.Exists(dll))
            {
                _log.Warn($"ข้าม extension {name} — ไม่พบไฟล์ {Path.GetFileName(dll)}");
                continue;
            }
            sb.AppendLine($"extension={name}");
        }

        if (php.OpCache)
        {
            var opcacheDll = Path.Combine(_p.PhpDir, "ext", "php_opcache.dll");
            if (File.Exists(opcacheDll))
            {
                sb.AppendLine($"zend_extension = \"{opcacheDll}\"");
                sb.AppendLine("opcache.enable = 1");
                sb.AppendLine("opcache.memory_consumption = 128");
                sb.AppendLine("opcache.max_accelerated_files = 10000");
            }
        }

        // Xdebug (เขียนเมื่อเปิดใช้ + มีไฟล์จริง — ใช้ debug กับ VS Code)
        var xdebugDll = Path.Combine(_p.PhpDir, "ext", "php_xdebug.dll");
        if (php.XdebugEnabled && File.Exists(xdebugDll))
        {
            sb.AppendLine($"zend_extension = \"{xdebugDll}\"");
            sb.AppendLine("xdebug.mode = debug");
            sb.AppendLine($"xdebug.client_port = {php.XdebugPort}");
            sb.AppendLine("xdebug.start_with_request = yes");
        }

        sb.AppendLine(end);

        WriteFile(_p.PhpIni, content.TrimEnd() + "\n" + sb.ToString());
        _log.Info("เขียน php.ini เรียบร้อย (managed block)");
    }

    // ================= MariaDB: my.ini =================
    public void WriteMariaDbIni()
    {
        BackupConfig(_p.MariaDbMyIni);
        var m = _c.MariaDb;
        var bindLine = string.IsNullOrWhiteSpace(m.BindAddress)
            ? "# (ไม่จำกัด — ฟังทุก interface)"
            : $"bind-address = {m.BindAddress}";

        var ini = $$"""
            # ============================================================
            # MariaDB configuration — สร้างโดย zServer Manager
            # สร้างเมื่อ: {{DateTime.Now:yyyy-MM-dd HH:mm:ss}}
            # ============================================================
            [mysqld]
            basedir = "{{Fw(_p.MariaDbDir)}}"
            datadir = "{{Fw(m.DataDir)}}"
            port = {{m.Port}}
            {{bindLine}}

            character-set-server = {{m.CharacterSet}}
            collation-server = {{m.Collation}}
            lower_case_table_names = {{m.LowerCaseTableNames}}

            innodb_buffer_pool_size = {{m.InnoDBBufferPoolSize}}
            key_buffer_size = {{m.KeyBufferSize}}
            max_connections = {{m.MaxConnections}}
            max_allowed_packet = 64M

            log_error = "{{Fw(_p.MariaDbErrorLog)}}"
            skip-name-resolve

            [client]
            port = {{m.Port}}
            default-character-set = {{m.CharacterSet}}

            [mysql]
            default-character-set = {{m.CharacterSet}}
            """;

        WriteFile(_p.MariaDbMyIni, ini);
        _log.Info($"เขียน my.ini เรียบร้อย (พอร์ต {m.Port})");
    }

    // ================= หน้าเว็บตัวอย่าง =================
    public void WriteIndexPage(bool overwrite = false)
    {
        var indexPhp = Path.Combine(_p.WwwDir, "index.php");
        if (File.Exists(indexPhp) && !overwrite) return;

        var html = """
            <?php
            /**
             * หน้าเว็บตัวอย่างของ zServer — ลบไฟล์นี้ได้เลยเมื่อเริ่มโปรเจกต์จริง
             */
            ?>
            <!DOCTYPE html>
            <html lang="th">
            <head>
            <meta charset="UTF-8">
            <title>zServer ทำงานแล้ว!</title>
            <style>
                body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #f4f6fb; color: #222; margin: 0; }
                .box { max-width: 760px; margin: 48px auto; background: #fff; border-radius: 12px;
                       padding: 32px 40px; box-shadow: 0 4px 18px rgba(0,0,0,.08); }
                h1 { color: #0a7d43; margin-top: 0; }
                code { background: #eef; padding: 2px 6px; border-radius: 4px; }
                ul { line-height: 1.8; }
            </style>
            </head>
            <body>
            <div class="box">
                <h1>&#9989; zServer ทำงานแล้ว!</h1>
                <p>Apache + PHP + MariaDB พร้อมใช้งาน ไฟล์เว็บของคุณวางไว้ที่โฟลเดอร์ <code><?= htmlspecialchars($_SERVER['DOCUMENT_ROOT']) ?></code></p>
                <h3>ข้อมูลจากระบบ:</h3>
                <ul>
                    <li>PHP เวอร์ชัน: <code><?= PHP_VERSION ?></code></li>
                    <li>วันที่เซิร์ฟเวอร์: <code><?= date('d/m/Y H:i:s') ?></code> (timezone: <?= date_default_timezone_get() ?>)</li>
                    <li>ฟังก์ชัน mysqli: <code><?= extension_loaded('mysqli') ? 'พร้อมใช้' : 'ยังไม่เปิด' ?></code></li>
                    <li>ฟังก์ชัน mbstring: <code><?= extension_loaded('mbstring') ? 'พร้อมใช้' : 'ยังไม่เปิด' ?></code></li>
                </ul>
                <p><a href="phpinfo.php">ดู phpinfo แบบเต็ม</a></p>
            </div>
            </body>
            </html>
            """;

        var phpinfo = """
            <?php
            // หน้า phpinfo — ควรลบไฟล์นี้เมื่อขึ้นเว็บจริง เพราะเปิดเผยข้อมูลระบบ
            phpinfo();
            """;

        WriteFile(indexPhp, html);
        WriteFile(Path.Combine(_p.WwwDir, "phpinfo.php"), phpinfo);
        _log.Info("สร้างหน้าเว็บตัวอย่างใน www\\ เรียบร้อย");
    }

    // ================= เขียนทั้งหมด =================
    public void WriteAll(bool includeIndexPage = true)
    {
        WriteApacheConf();
        if (File.Exists(_p.PhpBin)) WritePhpIni();
        if (File.Exists(_p.MariaDbBin)) WriteMariaDbIni();
        if (includeIndexPage) WriteIndexPage();
    }
}
