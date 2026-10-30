<#
.SYNOPSIS
    Builds the BusyLightStatus firmware and packages it as a single flashable image.

.DESCRIPTION
    1. Compiles the firmware with PlatformIO (environment m5stack-atom).
    2. Merges bootloader, partition table, boot_app0 and application into one .bin (offset 0x0).
    3. Calculates the SHA-256 hash.
    4. Writes manifest.json, used by the app to validate the device before flashing.

    Output: dist\firmware\
        busylight-atom-matrix-<version>-merged.bin
        manifest.json

.EXAMPLE
    .\scripts\build-firmware.ps1 -Version 5.0.0
.EXAMPLE
    .\scripts\build-firmware.ps1 -Version 5.0.0 -UsbIds "0403:6001" -SkipBuild
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$ProjectDir  = (Join-Path $PSScriptRoot '..\firmware\atom-matrix'),
    [string]$Environment = 'm5stack-atom',
    [string]$OutputDir   = (Join-Path $PSScriptRoot '..\dist\firmware'),
    [string[]]$UsbIds    = @(),
    [int]$FlashSizeMB    = 4,
    [int]$Baud           = 115200,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Fail($msg)       { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- tools
$pioHome  = Join-Path $env:USERPROFILE '.platformio'
$pioExe   = (Get-Command pio -ErrorAction SilentlyContinue).Source
if (-not $pioExe) { $pioExe = Join-Path $pioHome 'penv\Scripts\pio.exe' }
if (-not (Test-Path $pioExe)) { Fail "PlatformIO not found. Install PlatformIO Core (only needed on the build machine)." }

$pioPython = Join-Path $pioHome 'penv\Scripts\python.exe'
$esptoolPy = Join-Path $pioHome 'packages\tool-esptoolpy\esptool.py'
$bootApp0  = Join-Path $pioHome 'packages\framework-arduinoespressif32\tools\partitions\boot_app0.bin'

$ProjectDir = (Resolve-Path $ProjectDir).Path
$buildDir   = Join-Path $ProjectDir ".pio\build\$Environment"

# ---------------------------------------------------------------- version check
$mainCpp = Join-Path $ProjectDir 'src\main.cpp'
if (Test-Path $mainCpp) {
$m = Select-String -Path $mainCpp -Pattern 'FW_VERSION\s*=?\s*"([^"]+)"' | Select-Object -First 1
    if ($m) {
        $srcVersion = $m.Matches[0].Groups[1].Value
        if ($srcVersion -ne $Version) {
            Fail "FW_VERSION in main.cpp is '$srcVersion' but -Version is '$Version'. Align them first."
        }
    } else {
        Write-Warning "FW_VERSION not found in main.cpp. The app will not be able to confirm the version after flashing."
    }
}

# ---------------------------------------------------------------- 1. build
if (-not $SkipBuild) {
    Write-Step "Building firmware ($Environment)"
    & $pioExe run -d $ProjectDir -e $Environment
    if ($LASTEXITCODE -ne 0) { Fail "PlatformIO build failed." }
}

$parts = [ordered]@{
    '0x1000'  = Join-Path $buildDir 'bootloader.bin'
    '0x8000'  = Join-Path $buildDir 'partitions.bin'
    '0xe000'  = $bootApp0
    '0x10000' = Join-Path $buildDir 'firmware.bin'
}
foreach ($p in $parts.GetEnumerator()) {
    if (-not (Test-Path $p.Value)) { Fail "Missing part at $($p.Key): $($p.Value)" }
}

# ---------------------------------------------------------------- 2. merge
Write-Step "Merging images"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
Get-ChildItem $OutputDir -Filter '*.bin' | Remove-Item -Force

$binName = "busylight-atom-matrix-$Version-merged.bin"
$binPath = Join-Path $OutputDir $binName

$mergeArgs = @(
    '--chip', 'esp32', 'merge_bin', '-o', $binPath,
    '--flash_mode', 'keep', '--flash_freq', 'keep', '--flash_size', "$($FlashSizeMB)MB"
)
foreach ($p in $parts.GetEnumerator()) { $mergeArgs += @($p.Key, $p.Value) }

if ((Test-Path $pioPython) -and (Test-Path $esptoolPy)) {
    & $pioPython $esptoolPy @mergeArgs
} else {
    Write-Warning "esptool from PlatformIO not found, trying 'python -m esptool'."
    & python -m esptool @mergeArgs
}
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $binPath)) { Fail "esptool merge_bin failed." }

$size = (Get-Item $binPath).Length
if ($size -gt $FlashSizeMB * 1MB) { Fail "Merged image ($size bytes) is larger than $FlashSizeMB MB." }

# ---------------------------------------------------------------- 3. hash
Write-Step "Calculating SHA-256"
$sha256 = (Get-FileHash $binPath -Algorithm SHA256).Hash.ToLowerInvariant()

# ---------------------------------------------------------------- 4. manifest
Write-Step "Writing manifest.json"
$gitCommit = (git -C $ProjectDir rev-parse --short HEAD 2>$null)
$manifest = [ordered]@{
    name       = 'BusyLightStatus'
    version    = $Version
    chip       = 'ESP32'
    minFlashMB = $FlashSizeMB
    file       = $binName
    offset     = '0x0'
    baud       = $Baud
    size       = $size
    sha256     = $sha256
    usbIds     = $UsbIds
    buildDate  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    gitCommit  = "$gitCommit"
}
$manifestPath = Join-Path $OutputDir 'manifest.json'
$manifest | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath -Encoding utf8

# ---------------------------------------------------------------- summary
Write-Host "`nFirmware package ready" -ForegroundColor Green
Write-Host "  File    : $binPath"
Write-Host "  Size    : $size bytes"
Write-Host "  SHA-256 : $sha256"
Write-Host "  Manifest: $manifestPath"
if ($UsbIds.Count -eq 0) { Write-Warning "usbIds is empty. Pass -UsbIds with the VID:PID of your ATOM." }
