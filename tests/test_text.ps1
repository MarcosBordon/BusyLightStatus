$port = New-Object System.IO.Ports.SerialPort COM3,115200,None,8,one
$port.ReadTimeout = 2000
$port.DtrEnable = $false
$port.RtsEnable = $false
$port.Open()
Start-Sleep -Seconds 2
$port.DiscardInBuffer()

function Send($json, $label, $wait = 6) {
    $port.WriteLine($json)
    Start-Sleep -Milliseconds 300
    try { $r = $port.ReadLine() } catch { $r = "no response" }
    Write-Host "$(Get-Date -Format HH:mm:ss)  $label -> $r"
    Start-Sleep -Seconds $wait
}

Send '{"Cmd":"setText","Text":"HELLO","Color":"#00A0FF","Brightness":80}' "HELLO light blue (chars 5)" 8
Send '{"Cmd":"setText","Text":"LL OO","Color":"#FFFFFF","Brightness":60}' "LL OO white (chars 5)" 8
Send '{"Cmd":"setText","Text":"IN A MEETING","Random":true,"Brightness":80}' "IN A MEETING random (chars 12)" 14
Send '{"Cmd":"setText","Text":"ESPA\u00D1A","Color":"#FFC000","Brightness":80}' "ESPANA with N tilde (chars 6)" 9
Send '{"Cmd":"setText","Text":"reuni\u00F3n","Color":"#00FF00","Brightness":80}' "reunion with accent (chars 7)" 9
Send '{"Cmd":"setText","Text":"0123456789?!.-:","Random":true,"Brightness":80}' "Digits and symbols (chars 15)" 9
Send '{"Cmd":"setText","Text":"ABCDEFGHIJKLMNOPQRST","Color":"#FF00FF","Brightness":80}' "20 letters, truncated (chars 15)" 9
Send '{"Cmd":"setText","Text":"","Brightness":80}' "Empty text (empty_text)" 2
Send '{"Cmd":"setStatus","Color":"#00FF00","Effect":"solid","Brightness":100}' "Back to status: solid green" 3

$port.Close()
$port.Dispose()