# BadmintonBooking 1000-user load test (PowerShell fallback for k6).
# True concurrency via .NET HttpClient + Task (PS 5.1 has no -Parallel).
#   Phase 1: 1000 concurrent read fan-out (availability + admin summary)
#   Phase 2: 50-way same-slot booking burst (overbooking guard)
# Requires: API running on :5131, DB seeded.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$Api = "http://localhost:5131/api/v1"
$today = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")

function Get-Percentile([double[]]$Sorted, [double]$P) {
    if ($Sorted.Count -eq 0) { return 0 }
    $idx = [Math]::Ceiling($P * $Sorted.Count) - 1
    if ($idx -ge $Sorted.Count) { $idx = $Sorted.Count - 1 }
    return $Sorted[$idx]
}

# ── tokens (read status to diagnose 429 rate-limit / 401) ─────
$loginClient = [System.Net.Http.HttpClient]::new()
$loginClient.Timeout = [System.TimeSpan]::FromSeconds(30)
function Get-Token([string]$phone, [string]$pass) {
    $body = [System.Text.Encoding]::UTF8.GetBytes((@{ phoneNumber = $phone; password = $pass } | ConvertTo-Json))
    $ct = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, "application/json")
    $resp = $loginClient.PostAsync("$Api/auth/login", $ct).GetAwaiter().GetResult()
    $code = [int]$resp.StatusCode
    if ($code -ne 200) { Write-Output "   login $phone -> HTTP $code"; return $null }
    return (($resp.Content.ReadAsStringAsync()).GetAwaiter().GetResult() | ConvertFrom-Json).data.accessToken
}
$adminToken = Get-Token "0900000001" "Admin@123"
$custToken  = Get-Token "091000000"  "Pass@123"
if (-not $adminToken) {
    Write-Output "FATAL: admin login failed — is the API seeded & running on :5131?"
    exit 1
}
if (-not $custToken) { $custToken = $adminToken }

$adminAuth = "Bearer $adminToken"
$custAuth  = "Bearer $custToken"

# Helper: run a string of PS code (values baked in) as a concurrent task,
# returning a PSCustomObject. Parameterless scriptblock -> Func[object].
function Run-Parallel {
    param([System.Collections.Generic.List[string]]$codeList, [int]$throttle)
    $tasks = [System.Collections.Generic.List[object]]::new()
    foreach ($code in $codeList) {
        $sb = [scriptblock]::Create($code)
        $func = $sb.AsDelegate([Func[object]])
        $tasks.Add([System.Threading.Tasks.Task]::Run($func))
    }
    $out = [System.Collections.Generic.List[object]]::new()
    foreach ($t in $tasks) { $out.Add($t.GetAwaiter().GetResult()) }
    return $out
}

# ── Phase 1: 1000 concurrent read fan-out ───────────────────
Write-Output ""
Write-Output "=========================================================="
Write-Output " Phase 1: 1000 concurrent read fan-out"
Write-Output "=========================================================="

$N = 1000
$wall = [System.Diagnostics.Stopwatch]::StartNew()
$codeList = [System.Collections.Generic.List[string]]::new()
for ($i = 0; $i -lt $N; $i++) {
    if ($i % 2 -eq 0) { $url = "$Api/bookings/availability/1/$today"; $auth = $custAuth }
    else              { $url = "$Api/admin/report/summary";            $auth = $adminAuth }
    $urlLit  = "'" + $url + "'"
    $authLit = "'" + $auth + "'"
    $codeList.Add(@"
`$client = [System.Net.Http.HttpClient]::new()
`$client.Timeout = [System.TimeSpan]::FromSeconds(30)
`$client.DefaultRequestHeaders.Add('Authorization', $authLit)
`$sw = [System.Diagnostics.Stopwatch]::StartNew()
`$ok = `$false
try {
    `$r = `$client.GetAsync($urlLit).GetAwaiter().GetResult()
    `$ok = `$r.IsSuccessStatusCode
    [void]`$r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
} catch { `$ok = `$false }
`$sw.Stop()
`$client.Dispose()
[PSCustomObject]@{ Ms = [double]`$sw.Elapsed.TotalMilliseconds; Ok = `$ok }
"@)
}
$wall.Stop()
$results = Run-Parallel $codeList
$lat = @($results | ForEach-Object { [double]($_.Ms) } | Sort-Object)
$okCount = @($results | Where-Object { $_.Ok }).Count
$errCount = $N - $okCount

Write-Output (" Requests : {0}" -f $N)
Write-Output (" Success  : {0} ({1:P0})  Errors: {2}" -f $okCount, ($okCount / $N), $errCount)
Write-Output (" p50 = {0:N0} ms | p95 = {1:N0} ms | p99 = {2:N0} ms | max = {3:N0} ms" -f `
    (Get-Percentile $lat 0.50), (Get-Percentile $lat 0.95), (Get-Percentile $lat 0.99), ($lat[-1]))
Write-Output (" Throughput: {0:N0} req/s (wall {1:N1}s)" -f ($N / $wall.Elapsed.TotalSeconds), $wall.Elapsed.TotalSeconds)

# ── Phase 2: 50-way same-slot booking burst ──────────────────
Write-Output ""
Write-Output "=========================================================="
Write-Output " Phase 2: 50-way same-slot booking burst (overbooking)"
Write-Output "=========================================================="

$burstN = 50
$bList = [System.Collections.Generic.List[string]]::new()
for ($i = 0; $i -lt $burstN; $i++) {
    $apiLit = "'" + $Api + "'"
    $todayLit = "'" + $today + "'"
    $authLit = "'" + $custAuth + "'"
    $bList.Add(@"
`$client = [System.Net.Http.HttpClient]::new()
`$client.Timeout = [System.TimeSpan]::FromSeconds(30)
`$client.DefaultRequestHeaders.Add('Authorization', $authLit)
`$body = @{ courtId = 1; date = $todayLit; slotId = 7; playerCount = 1 } | ConvertTo-Json
`$content = [System.Net.Http.StringContent]::new(`$body, [System.Text.Encoding]::UTF8, 'application/json')
`$sw = [System.Diagnostics.Stopwatch]::StartNew()
`$status = 599
try {
    `$r = `$client.PostAsync(`$apiLit + '/bookings', `$content).GetAwaiter().GetResult()
    `$status = [int]`$r.StatusCode
    [void]`$r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
} catch { `$status = 599 }
`$sw.Stop()
`$client.Dispose()
[PSCustomObject]@{ Status = `$status; Ms = [double]`$sw.Elapsed.TotalMilliseconds }
"@)
}
$burst = Run-Parallel $bList
$winners   = @($burst | Where-Object { $_.Status -eq 200 }).Count
$conflicts = @($burst | Where-Object { $_.Status -eq 409 }).Count
$other     = $burstN - $winners - $conflicts
$bLat = @($burst | ForEach-Object { [double]($_.Ms) } | Sort-Object)

Write-Output (" 200 (winner)      : {0} (expect 1)" -f $winners)
Write-Output (" 409 SLOT_CONFLICT : {0} (expect {1})" -f $conflicts, ($burstN - 1))
Write-Output (" other statuses    : {0}" -f $other)
Write-Output (" burst p95 = {0:N0} ms" -f (Get-Percentile $bLat 0.95))

$pass = ($winners -eq 1) -and ($other -eq 0) -and (($okCount / $N) -ge 0.95)
Write-Output ""
Write-Output "=========================================================="
Write-Output (" RESULT: {0}" -f (if ($pass) {
    "PASS — 1000-user read fan-out OK, overbooking guard held"
} else {
    "REVIEW — check counts above"
}))
Write-Output "=========================================================="
