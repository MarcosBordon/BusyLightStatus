$port = New-Object System.IO.Ports.SerialPort COM3,115200,None,8,one
$port.ReadTimeout = 2000
$port.DtrEnable = $false
$port.RtsEnable = $false
$port.Open()
Start-Sleep -Seconds 2
$port.DiscardInBuffer()

function Send($json, $label) {
    $port.WriteLine($json)
    Start-Sleep -Milliseconds 300
    try { $r = $port.ReadLine() } catch { $r = "sin respuesta" }
    Write-Host "$label -> $r"
    Start-Sleep -Seconds 5
}

Send '{"Cmd":"identify"}' "Identificacion"
Send '{"Cmd":"setStatus","Color":"#00FF00","Effect":"solid","Brightness":100}' "Disponible (verde fijo)"
Send '{"Cmd":"setStatus","Color":"#FF0000","Effect":"solid","Brightness":100}' "Ocupado (rojo fijo)"
Send '{"Cmd":"setStatus","Color":"#FF0000","Effect":"pulse","Brightness":100}' "En reunion (rojo pulsante)"
Send '{"Cmd":"setStatus","Color":"#FF0000","Effect":"doublepulse","Brightness":100}' "No molestar (doble pulso)"
Send '{"Cmd":"setStatus","Color":"#FF00FF","Effect":"pulse","Brightness":100}' "Presentando (magenta)"
Send '{"Cmd":"setStatus","Color":"#FFC000","Effect":"solid","Brightness":100}' "Ausente (amarillo)"

$port.Close()
$port.Dispose()