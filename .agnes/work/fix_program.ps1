$p = 'D:\BadmintonBooking\Badminton\Badminton\Program.cs'
$c = [IO.File]::ReadAllText($p)
$old = "// Add all services to this`nbuilder.Services.AddConfig(builder.Configuration);"
$new = "builder.Services.AddHttpContextAccessor();"
if ($c.Contains($old)) {
    $c = $c.Replace($old, $new)
    [IO.File]::WriteAllText($p, $c)
    Write-Output 'REPLACED (CRLF path)'
} elseif ($c.Contains($old.Replace("`n", ""))) {
    $c = $c.Replace($old.Replace("`n",""), $new)
    [IO.File]::WriteAllText($p, $c)
    Write-Output 'REPLACED (LF path)'
} else {
    Write-Output 'PATTERN NOT FOUND'
}
Select-String -Path $p -Pattern 'AddConfig|AddHttpContextAccessor' | ForEach-Object { Write-Output ($_.LineNumber.ToString() + ': ' + $_.Line) }
