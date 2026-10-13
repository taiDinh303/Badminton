$ErrorActionPreference = "Stop"
$base = "http://localhost:5131/api/v1"
$pw   = "Hovaten123@"
$fail = 0
$tmp  = Join-Path $env:TEMP "bb_body.json"

function Write-Body($json) {
    Set-Content -Path $tmp -Value $json -Encoding UTF8 -NoNewline
}
function Get-Code($method, $url, $token, $body) {
    $args_ = @("-s", "-o", "NUL", "-w", "%{http_code}", "-X", $method)
    if ($token) { $args_ += @("-H", "Authorization: Bearer $token") }
    if ($body)  { Write-Body $body; $args_ += @("-H", "Content-Type: application/json", "--data", "@$tmp") }
    $args_ += $url
    $out = & curl.exe @args_ 2>&1
    if ($out -match "^\d{3}$") { return [int]$out } else { return 0 }
}
function Get-Json($method, $url, $token, $body) {
    $args_ = @("-s")
    if ($method -eq "POST") { $args_ += @("-X", "POST") }
    if ($token) { $args_ += @("-H", "Authorization: Bearer $token") }
    if ($body)  { Write-Body $body; $args_ += @("-H", "Content-Type: application/json", "--data", "@$tmp") }
    $args_ += $url
    return (& curl.exe @args_ 2>&1 | Out-String) | ConvertFrom-Json
}
function Check([string]$name, [int]$expected, [int]$actual) {
    if ($actual -eq $expected) { Write-Host "PASS  $name" }
    else { Write-Host "FAIL  $name (got $actual, want $expected)"; $script:fail++ }
}

$phones = @("0900000003","0900000004","0900000005","0900000006")
foreach ($phone in $phones) {
    $s = Get-Code POST "$base/auth/login" $null "{`"phoneNumber`:`"$phone`",`"password`:`"$pw`"}"
    Check "login $phone" 200 $s
}
$wrong = Get-Code POST "$base/auth/login" $null "{`"phoneNumber`:`"0900000003`",`"password`:`"sai`"}"
Check "login wrong-pw -> 400" 400 $wrong

$rej = Get-Code POST "$base/auth/register" $null "{`"fullName`:`"X`",`"phoneNumber`:`"0900000099`",`"password`:`"$pw`",`"role`:`"Admin`"}"
Check "self-register admin -> 409" 409 $rej

$adminTok = (Get-Json POST "$base/auth/login" $null "{`"phoneNumber`:`"0900000006`",`"password`:`"$pw`"}").data.accessToken
$custTok  = (Get-Json POST "$base/auth/login" $null "{`"phoneNumber`:`"0900000003`",`"password`:`"$pw`"}").data.accessToken
Write-Host "adminTok=$([bool]$adminTok) custTok=$([bool]$custTok)"

$s = Get-Code GET "$base/courts?branchId=1" $adminTok $null;  Check "courts" 200 $s
$s = Get-Code GET "$base/time-slots" $adminTok $null;          Check "time-slots" 200 $s
$s = Get-Code GET "$base/vouchers" $adminTok $null;            Check "vouchers" 200 $s
$s = Get-Code GET "$base/vouchers/WELCOME10/preview?amount=100000" $custTok $null; Check "voucher preview" 200 $s

$date = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
$idem = "e2e-" + [guid]::NewGuid().ToString('N').Substring(0,8)
$s = Get-Code POST "$base/bookings" $custTok "{`"courtId`:1,`"slotId`:1,`"date`:`"$date`",`"voucherCode`:`"WELCOME10`",`"idempotencyKey`:`"$idem`"}"
Check "create booking (voucher)" 200 $s

$s = Get-Code POST "$base/bookings" $custTok "{`"courtId`:1,`"slotId`:1,`"date`:`"$date`"}"
Check "duplicate slot -> 409" 409 $s

$s = Get-Code GET "$base/bookings/mine" $custTok $null
Check "booking mine" 200 $s

$myBk = (Get-Json GET "$base/bookings/mine" $custTok $null).data | Select-Object -First 1
$bkId = $myBk.id
Write-Host "first booking id=$bkId"
$s = Get-Code POST "$base/payments/$bkId/on-site" $adminTok "{`"method`:`"Cash`"}"
Check "pay on-site (cash)" 200 $s

Write-Host "----------"
if ($fail -eq 0) { Write-Host "ALL E2E PASSED" } else { Write-Host "$fail CHECK(S) FAILED" }
Remove-Item $tmp -ErrorAction SilentlyContinue
