$p = 'D:\BadmintonBooking\Badminton\Badminton\Program.cs'
$c = [IO.File]::ReadAllText($p)
$needle = "builder.Services.AddConfig(builder.Configuration);"
if ($c.Contains($needle)) {
    $c = $c.Replace($needle, "builder.Services.AddHttpContextAccessor();")
    [IO.File]::WriteAllText($p, $c)
    Write-Output 'REPLACED AddConfig line'
} else {
    Write-Output 'NOT FOUND'
}
Select-String -Path $p -Pattern 'AddConfig|AddHttpContextAccessor' | ForEach-Object { Write-Output ($_.LineNumber.ToString() + ': ' + $_.Line) }
