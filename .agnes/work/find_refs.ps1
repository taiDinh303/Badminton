$root = 'D:\BadmintonBooking\Badminton'
$files = Get-ChildItem -Path $root -Recurse -File -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
$terms = @('TimeFrame','CheckIn','CourtModelView','BookingModelView','TimeSlotModelView','Services.Base','Service\Court','Service\Booking','IndoorCourt','OutdoorCourt','Core.Base.BaseEntity','ICourt','IPricingStrategy','IPaymentProcessor','PaymentProcessorFactory','CourtCategory','BookingStatus','PaymentMethod')
foreach ($t in $terms) {
  $hits = $files | Where-Object { (Select-String -Path $_.FullName -Pattern ([regex]::Escape($t)) -ErrorAction SilentlyContinue) } | ForEach-Object { $_.FullName.Replace($root + '\','') }
  Write-Output ('### ' + $t + '  (' + $hits.Count + ')')
  $hits | Select-Object -First 14 | ForEach-Object { Write-Output ('   ' + $_) }
}
