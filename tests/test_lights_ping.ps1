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
    try { $r = $port.ReadLine() } catch { $r = "sin respuesta" }
    Write-Host "$(Get-Date -Format HH:mm:ss)  $label -> $r"
    Start-Sleep -Seconds $wait
}

$L = '{"Cmd":"setStatus","Pattern":"lights","Effect":"solid","Brightness":80,'

Send ($L + '"Top":"#000000","Middle":"#000000","Bottom":"#000000"}') "Offline (todo apagado)"
Send ($L + '"Top":"#000000","Middle":"#FFC000","Bottom":"#000000"}') "Away (centro amarillo)"
Send ($L + '"Top":"#FF0000","Middle":"#000000","Bottom":"#000000"}') "InACall (arriba rojo)"
Send ($L + '"Top":"#000000","Middle":"#000000","Bottom":"#00FF00"}') "Available (abajo verde)"
Send ($L + '"Top":"#FF00FF","Middle":"#FF00FF","Bottom":"#FF00FF"}') "Presenting (tres magenta)"
Send ($L + '"Top":"#FF0000","Middle":"#FFC000","Bottom":"#00FF00"}') "Custom (semaforo)" 0

Write-Host "Prueba de ping: 90 segundos. NO debe aparecer azul intermitente."
for ($i = 1; $i -le 5; $i++) {
    Start-Sleep -Seconds 18
    Send '{"Cmd":"ping"}' "Ping $i" 0
}

Write-Host "Fin. Cierro el puerto: en unos 60 s debe aparecer azul intermitente."
$port.Close()
$port.Dispose()