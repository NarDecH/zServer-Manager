# ทดสอบนำเข้าใบรับรองจริง + กู้คืน mkcert (รันแบบ admin ด้วย pwsh 7)
$ErrorActionPreference = 'Continue'
Add-Type -Path 'C:\zServer\src\ZServer.Core\bin\Debug\net10.0-windows\ZServer.Core.dll'
$p = [ZServer.Core.ServerPaths]::new('C:\zServer')
$cfg = [ZServer.Core.AppConfig]::Load($p)
$log = [ZServer.Core.Logger]::new($p.ManagerLog)
$dl = [ZServer.Core.Downloader]::new($log)
$ct = [System.Threading.CancellationToken]::None
$ssl = 'C:\zServer\apache\bin\openssl.exe'
$cnf = 'C:\zServer\apache\conf\openssl.cnf'
if (-not (Test-Path $cnf)) { $cnf = (Get-ChildItem 'C:\zServer\apache' -Recurse -Filter 'openssl.cnf' | Select-Object -First 1).FullName }
$out = @()
$out += '[0] openssl.cnf: ' + $cnf

# สร้างใบรับรองจำลองแบบที่ผู้ขายให้มา (แยกไฟล์ + pfx)
& $ssl req -x509 -newkey rsa:2048 -keyout 'C:\zServer\tools\test-com.key' -out 'C:\zServer\tools\test-com.crt' -days 30 -nodes -subj '/CN=example.com' -config $cnf 2>&1 | Out-Null
& $ssl genrsa -out 'C:\zServer\tools\test-wrong.key' 2048 2>&1 | Out-Null
& $ssl pkcs12 -export -in 'C:\zServer\tools\test-com.crt' -inkey 'C:\zServer\tools\test-com.key' -out 'C:\zServer\tools\test-com.pfx' -passout pass:Test123 2>&1 | Out-Null
$out += '[0b] ไฟล์ทดสอบ: crt=' + (Test-Path 'C:\zServer\tools\test-com.crt') + ' key=' + (Test-Path 'C:\zServer\tools\test-com.key') + ' wrong=' + (Test-Path 'C:\zServer\tools\test-wrong.key') + ' pfx=' + (Test-Path 'C:\zServer\tools\test-com.pfx')

try {
    # [1] แบบ 2 ไฟล์ (.crt + .key)
    $r1 = [ZServer.Core.HttpsHelper]::ImportCommercialCert($p, $log, 'C:\zServer\tools\test-com.crt', 'C:\zServer\tools\test-com.key', $null, $null)
    $out += '[1] 2-file import: ok=' + $r1.Ok + ' | subject: ' + $r1.Info.Subject + ' | โดเมน: ' + (($r1.Info.Domains) -join ',')
} catch { $out += '[1] ERROR: ' + $_.Exception.Message }

try {
    # [2] key ผิด (ต้อง fail)
    $r2 = [ZServer.Core.HttpsHelper]::ImportCommercialCert($p, $log, 'C:\zServer\tools\test-com.crt', 'C:\zServer\tools\test-wrong.key', $null, $null)
    $out += '[2] key ผิด: ผ่าน?! (ควร fail) — BUG'
} catch { $out += '[2] key ผิดถูกจับ: ' + ($_.Exception.Message.Split("`n")[0]) }

try {
    # [3] แบบ PFX + รหัสผ่าน
    $r3 = [ZServer.Core.HttpsHelper]::ImportCommercialCert($p, $log, 'C:\zServer\tools\test-com.pfx', $null, $null, 'Test123')
    $out += '[3] pfx import: ok=' + $r3.Ok + ' | subject: ' + $r3.Info.Subject + ' | โดเมน: ' + (($r3.Info.Domains) -join ',')
} catch { $out += '[3] ERROR: ' + $_.Exception.Message }

# กู้คืน mkcert กลับ (ใบรับรอง dev ที่ localhost เชื่อ)
try {
    [string[]]$names = @('localhost')
    [ZServer.Core.HttpsHelper]::SetupAsync($p, $log, $dl, $names, $ct).GetAwaiter().GetResult()
    $r4 = [ZServer.Core.HttpsHelper]::GetCertInfo($p)
    $out += '[4] กู้คืน mkcert: ' + $r4.Subject + ' | โดเมน: ' + (($r4.Domains) -join ',')
} catch { $out += '[4] ERROR: ' + $_.Exception.Message }

Remove-Item 'C:\zServer\tools\test-com.*','C:\zServer\tools\test-wrong.key' -ErrorAction SilentlyContinue
$out | Set-Content 'C:\zServer\logs\import-cert-test-result.txt'
