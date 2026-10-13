Add-Type -Path 'D:\BadmintonBooking\Badminton\Badminton\bin\Debug\net8.0\BCrypt-Net-Next.dll'
$h = [BCrypt.Net.BCrypt]::HashPassword('Hovaten123@')
$v = [BCrypt.Net.BCrypt]::Verify('Hovaten123@', $h)
Write-Output "HASH=$h"
Write-Output "VERIFIED=$v"
