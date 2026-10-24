# Build the backend solution; optionally stop the API or run it afterwards.
param([switch]$Run, [switch]$StopApi)

$ErrorActionPreference = 'Stop'
$solutionPath = Join-Path $PSScriptRoot 'Backend/Badminton.slnx'
$apiProjectPath = Join-Path $PSScriptRoot 'Backend/API/Badminton.API.csproj'

if ($StopApi) {
    $apiOutputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Backend/API/bin')) + [IO.Path]::DirectorySeparatorChar
    foreach ($apiProcess in Get-Process -Name 'Badminton.API' -ErrorAction SilentlyContinue) {
        if ($apiProcess.Path -and $apiProcess.Path.StartsWith($apiOutputRoot, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "Stopping this checkout's API (PID $($apiProcess.Id))."
            Stop-Process -InputObject $apiProcess -Force
            Wait-Process -InputObject $apiProcess -Timeout 10 -ErrorAction Stop
        }
    }
}

if (($Run -or $StopApi) -and (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue)) {
    $listeners = @(Get-NetTCPConnection -LocalPort 5131 -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        $ownerIds = ($listeners.OwningProcess | Sort-Object -Unique) -join ', '
        throw "Port 5131 is already in use (PID: $ownerIds). Stop the existing API with Ctrl+C / Visual Studio Shift+F5. For this checkout's API, run .\build.ps1 -StopApi. Only run one API instance: Visual Studio OR dotnet run. Other processes are not stopped automatically."
    }
}

dotnet build $solutionPath -c Debug -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Run) {
    dotnet run --project $apiProjectPath -c Debug --no-build --launch-profile http
    exit $LASTEXITCODE
}
