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
    try { $r = $port.ReadLine() } catch { $r = "no response" }
    Write-Host "$label -> $r"
    Start-Sleep -Seconds 5
}

$R = '{"Cmd":"setStatus","Pattern":"rounded",'
Send ($R + '"Color":"#00FF00","Effect":"solid","Brightness":95}')        "Available"
Send ($R + '"Color":"#FF0000","Effect":"solid","Brightness":95}')        "Busy"
Send ($R + '"Color":"#FF0000","Effect":"pulse","Brightness":95}')        "In a meeting"
Send ($R + '"Color":"#FF0000","Effect":"doublepulse","Brightness":100}') "Do not disturb"
Send ($R + '"Color":"#FF00FF","Effect":"pulse","Brightness":100}')       "Presenting"
Send ($R + '"Color":"#FFC000","Effect":"solid","Brightness":70}')        "Away"

$port.Close()
$port.Dispose()