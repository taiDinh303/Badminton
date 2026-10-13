$p = 'D:\BadmintonBooking\Badminton\Badminton\Program.cs'
$lines = [IO.File]::ReadAllLines($p)
$new = $lines | Where-Object { $_ -ne 'using API;' -and $_ -ne 'using API.Middleware;' }
[IO.File]::WriteAllLines($p, $new)
Write-Output ('Removed stale usings. New first line: ' + $new[0])
