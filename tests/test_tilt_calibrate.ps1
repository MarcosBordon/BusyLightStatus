# Tilt axes calibration (FW 1.4.0-beta.2+)
# For each position: get ready, hold STILL during the reading (about 1 s).
# At the end, the script prints the values for TILT_SWAP_XY, TILT_INVERT_X and TILT_INVERT_Y.
# Close the tray app first so the COM port is free.

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
    try { return ($port.ReadLine() | ConvertFrom-Json) } catch { return $null }
}

# Average of 10 readings while the ATOM is held still
function Measure-Position($label) {
    Write-Host ""
    Write-Host ">>> $label" -ForegroundColor Yellow
    for ($c = 3; $c -ge 1; $c--) { Write-Host "    get ready... $c"; Start-Sleep -Seconds 1 }
    Write-Host "    HOLD STILL" -ForegroundColor Cyan

    $xs = @(); $ys = @(); $zs = @()
    for ($i = 0; $i -lt 10; $i++) {
        $r = Read-Tilt
        if ($r -and $r.imu) { $xs += [double]$r.x; $ys += [double]$r.y; $zs += [double]$r.z }
        Start-Sleep -Milliseconds 100
    }
    if ($xs.Count -eq 0) { throw "No sensor readings. Check that imu is true." }

    $p = [pscustomobject]@{
        X = ($xs | Measure-Object -Average).Average
        Y = ($ys | Measure-Object -Average).Average
        Z = ($zs | Measure-Object -Average).Average
        Spread = (($xs | Measure-Object -Maximum).Maximum - ($xs | Measure-Object -Minimum).Minimum) +
                 (($ys | Measure-Object -Maximum).Maximum - ($ys | Measure-Object -Minimum).Minimum)
    }
    "    x={0,6:N2}  y={1,6:N2}  z={2,6:N2}  (movement {3:N2})" -f $p.X, $p.Y, $p.Z, $p.Spread | Write-Host
    if ($p.Spread -gt 0.15) { Write-Host "    Warning: the ATOM moved during the reading." -ForegroundColor Red }
    return $p
}

Write-Host "Place the ATOM FLAT on the table, LEDs facing up, USB cable pointing AWAY from you."
Write-Host "Keep that orientation for every step."

$n = Measure-Position "FLAT on the table (neutral)"
$l = Measure-Position "Tilt about 30 degrees to the LEFT"
$r = Measure-Position "Tilt about 30 degrees to the RIGHT"
$a = Measure-Position "Tilt about 30 degrees AWAY from you (top row down)"
$t = Measure-Position "Tilt about 30 degrees TOWARD you (bottom row down)"

$port.Close()
$port.Dispose()

# Same math as the firmware: delta from neutral, optional swap, then inversion
function Delta($p) { return @(($p.X - $n.X), ($p.Y - $n.Y)) }
$dl = Delta $l; $dr = Delta $r; $da = Delta $a; $dt = Delta $t

# Left/right should move mostly one axis: if it is Y, axes must be swapped
$lrX = [math]::Abs($dr[0] - $dl[0])
$lrY = [math]::Abs($dr[1] - $dl[1])
$swap = $lrY -gt $lrX

function Map($d) { if ($swap) { return @($d[1], $d[0]) } else { return @($d[0], $d[1]) } }
$mr = Map $dr; $ml = Map $dl; $ma = Map $da; $mt = Map $dt

# Firmware: dx > 0 = RIGHT, dy < 0 = UP (top row)
$invertX = ($mr[0] - $ml[0]) -lt 0
$invertY = ($ma[1] - $mt[1]) -gt 0

$lrSignal = [math]::Max($lrX, $lrY)
$udSignal = [math]::Abs($ma[1] - $mt[1])

Write-Host ""
Write-Host "================ RESULT ================" -ForegroundColor Green
Write-Host ("Left/right signal: {0:N2} g   Away/toward signal: {1:N2} g" -f $lrSignal, $udSignal)
if ($lrSignal -lt 0.5 -or $udSignal -lt 0.5) {
    Write-Host "Signals are weak: tilt more (about 30 degrees) and run the test again." -ForegroundColor Red
}
Write-Host ""
Write-Host "Copy these lines into main.cpp:"
Write-Host ("static const bool TILT_SWAP_XY  = {0};" -f $swap.ToString().ToLower())
Write-Host ("static const bool TILT_INVERT_X = {0};" -f $invertX.ToString().ToLower())
Write-Host ("static const bool TILT_INVERT_Y = {0};" -f $invertY.ToString().ToLower())
