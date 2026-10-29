# Tilt sensor axes test (FW 1.4.0-beta.2+)
# Prints the accelerometer every 0.5 s for 30 s. Close the tray app first.
# Follow the prompts and note which value changes and its sign.

$port = New-Object System.IO.Ports.SerialPort COM3,115200,None,8,one
$port.ReadTimeout = 2000
$port.DtrEnable = $false
$port.RtsEnable = $false
$port.Open()
Start-Sleep -Seconds 2
$port.DiscardInBuffer()

function Read-Tilt {
    $port.DiscardInBuffer()
    $port.WriteLine('{"Cmd":"tiltRead"}')
    try { return $port.ReadLine() } catch { return "no response" }
}

$steps = @(
    "Hold the ATOM flat, LEDs facing up (neutral)",
    "Tilt it to the LEFT",
    "Tilt it to the RIGHT",
    "Tilt it AWAY from you (top row down)",
    "Tilt it TOWARD you (bottom row down)"
)

foreach ($s in $steps) {
    Write-Host ""
    Write-Host ">>> $s" -ForegroundColor Yellow
    for ($i = 0; $i -lt 12; $i++) {
        Write-Host "    $(Read-Tilt)"
        Start-Sleep -Milliseconds 500
    }
}

$port.Close()
$port.Dispose()
