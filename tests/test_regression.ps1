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

Send '{"Cmd":"identify"}' "Identify"
Send '{"Cmd":"setStatus","Color":"#00FF00","Effect":"solid","Brightness":100}' "Available (solid green)"
Send '{"Cmd":"setStatus","Color":"#FF0000","Effect":"solid","Brightness":100}' "Busy (solid red)"
Send '{"Cmd":"setStatus","Color":"#FF0000","Effect":"pulse","Brightness":100}' "In a meeting (pulsing red)"
Send '{"Cmd":"setStatus","Color":"#FF0000","Effect":"doublepulse","Brightness":100}' "Do not disturb (double pulse)"
Send '{"Cmd":"setStatus","Color":"#FF00FF","Effect":"pulse","Brightness":100}' "Presenting (magenta)"
Send '{"Cmd":"setStatus","Color":"#FFC000","Effect":"solid","Brightness":100}' "Away (yellow)"

$port.Close()
$port.Dispose()