# Reset DB + start API + wait until listening
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-NetTCPConnection -LocalPort 5131 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 2
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "IF DB_ID('BadmintonBooking') IS NOT NULL BEGIN ALTER DATABASE BadmintonBooking SET SINGLE_USER WITH ROLLBACK IMMEDIATE; END" 2>&1 | Out-Null
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE BadmintonBooking" 2>&1 | Out-Null
Write-Output "DB dropped"

$repositoryPath = Split-Path -Parent $PSScriptRoot
$apiProjectPath = Join-Path $repositoryPath 'Backend/API/Badminton.API.csproj'
$apiOutPath = Join-Path $PSScriptRoot 'api_out.log'
$apiErrPath = Join-Path $PSScriptRoot 'api_err.log'
Set-Location -LiteralPath $repositoryPath
$env:ASPNETCORE_ENVIRONMENT = "Development"
$p = Start-Process -FilePath "dotnet" `
  -ArgumentList "run","--project","`"$apiProjectPath`"","--no-launch-profile","--urls","http://localhost:5131" `
  -PassThru -WindowStyle Hidden `
  -RedirectStandardOutput $apiOutPath `
  -RedirectStandardError $apiErrPath

# Wait until port 5131 is listening (max 120s; BCrypt seed of 500 users takes ~60s)
$deadline = (Get-Date).AddSeconds(120)
$listening = $false
while ((Get-Date) -lt $deadline) {
  $c = Get-NetTCPConnection -LocalPort 5131 -State Listen -ErrorAction SilentlyContinue
  if ($c) { $listening = $true; break }
  Start-Sleep -Seconds 5
}
if ($listening) {
  Write-Output "API LISTENING on :5131 (pid=$($p.Id))"
} else {
  Write-Output "API NOT LISTENING — tail of log:"
  Get-Content $apiErrPath -Tail 8 -ErrorAction SilentlyContinue
  Get-Content $apiOutPath -Tail 5 -ErrorAction SilentlyContinue
}
