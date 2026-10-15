# Stop any running BadmintonBE.API (it locks bin DLLs and blocks MSBuild copy),
# then build. Optionally -Run to start the API afterwards.
param([switch]$Run)

Get-Process -Name BadmintonBE.API -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping API pid $($_.Id)"
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Milliseconds 500

Set-Location $PSScriptRoot\Badminton
dotnet build Badminton.slnx -c Debug -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

if ($Run) {
    Write-Host "Starting API on :5131 (Dev)"
    dotnet run --project Badminton -c Debug --no-build
}
