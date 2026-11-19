$ErrorActionPreference = 'Continue'
Add-Type -Path 'C:\zServer\src\ZServer.Core\bin\Debug\net10.0-windows\ZServer.Core.dll'
$p = [ZServer.Core.ServerPaths]::new('C:\zServer')
$cfg = [ZServer.Core.AppConfig]::Load($p)
$log = [ZServer.Core.Logger]::new($p.ManagerLog)
$dl = [ZServer.Core.Downloader]::new($log)
$ct = [System.Threading.CancellationToken]::None
$out = @()
try {
    [string[]]$names = @('localhost')
    [ZServer.Core.HttpsHelper]::SetupAsync($p, $log, $dl, $names, $ct).GetAwaiter().GetResult()
    $info = [ZServer.Core.HttpsHelper]::GetCertInfo($p)
    $out += '[1] subject: ' + $info.Subject + ' | หมดอายุ: ' + $info.NotAfter
    $out += '[2] โดเมน: ' + (($info.Domains) -join ', ')
    $cfg.Apache.EnableHttps = $true
    $cfg.Save($p)
    [ZServer.Core.ConfigWriter]::new($p, $cfg, $log).WriteApacheConf()
    $out += '[3] เขียน config เปิด https (รวม vhost) แล้ว'
} catch { $out += 'ERROR: ' + $_.Exception.Message }
$out | Set-Content 'C:\zServer\logs\https-test-result.txt'
