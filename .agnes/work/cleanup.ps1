$targets = @(
  'Core\Abstractions','Core\Enums','Core\Strategies',
  'Contract.Repositories\Entity\TimeFrame.cs','Contract.Repositories\Entity\CheckIn.cs',
  'ModelViews\CourtModelView','ModelViews\BookingModelView','ModelViews\TimeSlotModelView',
  'Contract.Services\Interface\Booking','Services\Base','Services\Service\Court','Services\Service\Booking',
  'Services\Mappings\BookingMapping.cs'
)
foreach ($t in $targets) {
  $full = Join-Path 'D:\BadmintonBooking\Badminton' $t
  if (Test-Path $full) { Remove-Item $full -Recurse -Force; Write-Output ('DEL ' + $t) }
  else { Write-Output ('MISS ' + $t) }
}
