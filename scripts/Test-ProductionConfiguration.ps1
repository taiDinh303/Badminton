param([switch]$NoPublish)
$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publishPath = Join-Path $repositoryPath 'artifacts/configuration-check'
$projectPath = Join-Path $repositoryPath 'Backend/API/Badminton.API.csproj'
if (-not $NoPublish) {
    dotnet publish $projectPath -c Release --no-restore -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
}
if (Test-Path (Join-Path $publishPath 'appsettings.Development.json')) {
    throw 'Development configuration must not be published.'
}

function Invoke-SyntheticCheck([string]$signingKey) {
    $start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $publishPath
    $start.ArgumentList.Add((Join-Path $publishPath 'Badminton.API.dll'))
    $start.ArgumentList.Add('--check-config')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # No private parent configuration may influence this synthetic check.
    foreach ($name in @($start.Environment.Keys)) {
        if ($name -match '^(ConnectionStrings|JwtSettings|Cors|Payment|Notify|Database|DatabaseInitialization|Seed|Membership|BackgroundJobs|Swagger)__') {
            $start.Environment.Remove($name) | Out-Null
        }
    }
    $settings = @{
        ASPNETCORE_ENVIRONMENT = 'Production'
        DOTNET_ENVIRONMENT = 'Production'
        ConnectionStrings__BadmintonBooking = 'Server=127.0.0.1;Database=BadmintonConfigurationOnly;Integrated Security=true;Encrypt=true;'
        JwtSettings__Key = $signingKey
        Cors__AllowedOrigins__0 = 'https://booking.example.invalid'
        Payment__Provider = 'real'
        Payment__Momo__AppId = 'synthetic'
        Payment__Momo__SecretKey = 'synthetic'
        Payment__Momo__AccessKey = 'synthetic'
        Payment__Momo__IpayUrl = 'https://api.example.invalid/api/v1/payments/callback/momo'
        Payment__Zalopay__CallbackUrl = 'https://api.example.invalid/api/v1/payments/callback/zalopay'
        Payment__Vnpay__CallbackUrl = 'https://api.example.invalid/api/v1/payments/callback/vnpay'
        Payment__Momo__Endpoint = 'https://momo.example.invalid'
        Payment__Vnpay__MerchantCode = 'synthetic'
        Payment__Vnpay__SecretKey = 'synthetic'
        Payment__Vnpay__Endpoint = 'https://vnpay.example.invalid'
        Payment__Zalopay__AppId = 'synthetic'
        Payment__Zalopay__ApiKey = 'synthetic'
        Payment__Zalopay__ApiSecret = 'synthetic'
        Payment__Zalopay__CallbackKey = 'synthetic'
        Payment__Zalopay__Endpoint = 'https://zalopay.example.invalid'
        Notify__Channels = 'log'
    }
    foreach ($pair in $settings.GetEnumerator()) { $start.Environment[$pair.Key] = $pair.Value }
    $process = [System.Diagnostics.Process]::Start($start)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Dry-run timed out; it must not contact a database.' }
    $result = @{ Code = $process.ExitCode; Output = $stdoutTask.Result + $stderrTask.Result }
    $process.Dispose()
    return $result
}

$valid = Invoke-SyntheticCheck ('x' * 64)
if ($valid.Code -ne 0 -or $valid.Output -notmatch 'CONFIGURATION_OK') { throw 'Valid synthetic Production configuration failed.' }
$invalid = Invoke-SyntheticCheck 'short'
if ($invalid.Code -ne 2 -or $invalid.Output -notmatch 'CONFIGURATION_INVALID' -or $invalid.Output.Contains('short')) {
    throw 'Invalid synthetic configuration did not fail safely.'
}
Write-Output 'Production configuration checks passed: clean publish, valid dry-run, invalid fail-closed. No database or external service was used.'
