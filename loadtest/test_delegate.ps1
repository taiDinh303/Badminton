Add-Type -AssemblyName System.Net.Http
$code = '$x = 42; [PSCustomObject]@{ V = $x }'

Write-Output "A: AsDelegate(Func[object])"
try {
  $f = [scriptblock]::Create($code).AsDelegate([Func[object]])
  $v = & $f
  Write-Output "  ok -> $($v.V)"
} catch { Write-Output ("  FAIL " + $_.Exception.Message) }

Write-Output "B: cast [Func[object]]"
try {
  $f2 = [Func[object]]([scriptblock]::Create($code))
  Write-Output ("  ok -> " + $f2())
} catch { Write-Output ("  FAIL " + $_.Exception.Message) }

Write-Output "C: parameterless cast [Action]"
try {
  $a = [Action]([scriptblock]::Create('$y=1'))
  Write-Output ("  ok Action created")
} catch { Write-Output ("  FAIL " + $_.Exception.Message) }
