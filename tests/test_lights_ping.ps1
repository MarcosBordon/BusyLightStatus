$port = New-Object System.IO.Ports.SerialPort COM3,115200,None,8,one
$port.ReadTimeout = 2000
$port.DtrEnable = $false
$port.RtsEnable = $false
$port.Open()
Start-Sleep -Seconds 2
$port.DiscardInBuffer()

function Send($json, $label, $wait = 5) {
    $port.WriteLine($json)
    Start-Sleep -Milliseconds 300
    try { $r = $port.ReadLine() } catch { $r = "no response" }
    Write-Host "$(Get-Date -Format HH:mm:ss)  $label -> $r"
    Start-Sleep -Seconds $wait
}

$L = '{"Cmd":"setStatus","Pattern":"lights","Effect":"solid","Brightness":80,'

Send ($L + '"Top":"#000000","Middle":"#000000","Bottom":"#000000"}') "Offline (all off)"
Send ($L + '"Top":"#000000","Middle":"#FFC000","Bottom":"#000000"}') "Away (yellow middle)"
Send ($L + '"Top":"#FF0000","Middle":"#000000","Bottom":"#000000"}') "In a call (red top)"
Send ($L + '"Top":"#000000","Middle":"#000000","Bottom":"#00FF00"}') "Available (green bottom)"
Send ($L + '"Top":"#FF00FF","Middle":"#FF00FF","Bottom":"#FF00FF"}') "Presenting (three magenta)"
Send ($L + '"Top":"#FF0000","Middle":"#FFC000","Bottom":"#00FF00"}') "Full traffic light" 0

Write-Host "Ping test: 90 seconds. Blinking blue must NOT appear."
for ($i = 1; $i -le 5; $i++) {
    Start-Sleep -Seconds 18
    Send '{"Cmd":"ping"}' "Ping $i" 0
}

Write-Host "Done. Closing the port: blinking blue should appear in about 60 s."
$port.Close()
$port.Dispose()