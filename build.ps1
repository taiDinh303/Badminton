# Build the backend solution; optionally stop the API or run it afterwards.
param([switch]$Run, [switch]$StopApi)

$ErrorActionPreference = 'Stop'
$solutionPath = Join-Path $PSScriptRoot 'Backend/Badminton.slnx'
$apiProjectPath = Join-Path $PSScriptRoot 'Backend/API/Badminton.API.csproj'

if ($StopApi) {
    Get-Process -Name 'Badminton.API', 'BadmintonBE.API' -ErrorAction SilentlyContinue |
        Stop-Process -Force
}

dotnet build $solutionPath -c Debug -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Run) {
    dotnet run --project $apiProjectPath -c Debug --no-build --launch-profile http
    exit $LASTEXITCODE
}
