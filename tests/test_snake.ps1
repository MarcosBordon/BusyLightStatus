# Snake game test from PowerShell (FW 1.4.0+)
# Keys:  powershell -ExecutionPolicy Bypass -File .\tests\test_snake.ps1
# Tilt:  powershell -ExecutionPolicy Bypass -File .\tests\test_snake.ps1 -Tilt
# Arrows or WASD to play (also in tilt mode), Q to quit. Close the tray app first.

param([switch]$Tilt)

$port = New-Object System.IO.Ports.SerialPort COM3,115200,None,8,one
$port.ReadTimeout = 2000
$port.DtrEnable = $false
$port.RtsEnable = $false
$port.Open()
Start-Sleep -Seconds 2
$port.DiscardInBuffer()

function Cmd($json) {
    $port.DiscardInBuffer()
    $port.WriteLine($json)
    try { return $port.ReadLine() } catch { return "no response" }
}

$control = if ($Tilt) { "tilt" } else { "keys" }
if ($Tilt) { Write-Host "Hold the ATOM in the position you want as neutral..."; Start-Sleep -Seconds 2 }

Write-Host "Start -> $(Cmd ('{"Cmd":"gameStart","Speed":450,"Control":"' + $control + '"}'))"
Write-Host "Arrows or WASD to play, Q to quit."

$last = ""
while ($true) {
    if ([Console]::KeyAvailable) {
        $key = [Console]::ReadKey($true).Key
        $dir = switch ($key) {
            'UpArrow'    { 'up' }    'W' { 'up' }
            'DownArrow'  { 'down' }  'S' { 'down' }
            'LeftArrow'  { 'left' }  'A' { 'left' }
            'RightArrow' { 'right' } 'D' { 'right' }
            'Q'          { 'quit' }
            default      { $null }
        }
        if ($dir -eq 'quit') { break }
        if ($dir) { $null = Cmd ('{"Cmd":"gameDir","Dir":"' + $dir + '"}') }
    }

    $r = Cmd '{"Cmd":"gameState"}'
    if ($r -ne $last) { Write-Host "$(Get-Date -Format HH:mm:ss)  $r"; $last = $r }
    if ($r -match '"state":"(over|win)"') { Start-Sleep -Seconds 4; break }

    Start-Sleep -Milliseconds 100
}

Write-Host "Stop -> $(Cmd '{"Cmd":"gameStop"}')"
$port.Close()
$port.Dispose()
