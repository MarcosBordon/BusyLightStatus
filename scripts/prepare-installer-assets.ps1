<#
.SYNOPSIS
    Prepares the files the installer needs to flash the ATOM without developer tools.

.DESCRIPTION
    1. Downloads the official esptool Windows release (standalone .exe, no Python needed).
    2. Downloads the esptool GPLv2 license.
    3. Copies the packaged firmware (.bin + manifest.json) from dist\firmware.
    4. Validates esptool.exe and the firmware SHA-256 against the manifest.

    Output: dist\installer-assets\
        tools\esptool\esptool.exe (+ files from the release)
        tools\esptool\LICENSE.txt
        firmware\busylight-atom-matrix-<version>-merged.bin
        firmware\manifest.json

.EXAMPLE
    .\scripts\prepare-installer-assets.ps1
.EXAMPLE
    .\scripts\prepare-installer-assets.ps1 -EsptoolVersion 4.8.1 -Force
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$EsptoolVersion = '4.8.1',
    [string]$FirmwareDir    = (Join-Path $PSScriptRoot '..\dist\firmware'),
    [string]$OutputDir      = (Join-Path $PSScriptRoot '..\dist\installer-assets'),
    [string]$CacheDir       = (Join-Path $PSScriptRoot '..\.cache'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'   # much faster Invoke-WebRequest

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Fail($msg)       { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Force -Path $OutputDir, $CacheDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
$CacheDir  = (Resolve-Path $CacheDir).Path

$toolsOut    = Join-Path $OutputDir 'tools\esptool'
$firmwareOut = Join-Path $OutputDir 'firmware'
if (Test-Path $toolsOut)    { Remove-Item $toolsOut -Recurse -Force }
if (Test-Path $firmwareOut) { Remove-Item $firmwareOut -Recurse -Force }
New-Item -ItemType Directory -Force -Path $toolsOut, $firmwareOut | Out-Null

# ---------------------------------------------------------------- 1. esptool.exe
Write-Step "esptool v$EsptoolVersion (Windows)"
$zipName = "esptool-v$EsptoolVersion-win64.zip"
$zipPath = Join-Path $CacheDir $zipName
$zipUrl  = "https://github.com/espressif/esptool/releases/download/v$EsptoolVersion/$zipName"

if ($Force -or -not (Test-Path $zipPath)) {
    Write-Host "Downloading $zipUrl"
    try { Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing }
    catch { Fail "Download failed. Check the version on https://github.com/espressif/esptool/releases or download $zipName manually into $CacheDir." }
} else {
    Write-Host "Using cached $zipPath"
}

$extract = Join-Path $CacheDir "esptool-$EsptoolVersion"
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zipPath -DestinationPath $extract

$exe = Get-ChildItem $extract -Recurse -Filter 'esptool.exe' | Select-Object -First 1
if (-not $exe) { Fail "esptool.exe not found inside $zipName." }

# Copy the whole release folder (the exe may depend on files next to it)
Copy-Item -Path (Join-Path $exe.DirectoryName '*') -Destination $toolsOut -Recurse -Force
$esptoolExe = Join-Path $toolsOut 'esptool.exe'

$verOut = (& $esptoolExe version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $verOut -notmatch [regex]::Escape($EsptoolVersion)) {
    Fail "esptool.exe did not run correctly. Output: $verOut"
}
Write-Host "esptool.exe OK: $($verOut -split "`n" | Select-Object -Last 1)"

# ---------------------------------------------------------------- 2. license
Write-Step "esptool license (GPLv2)"
$licUrl = "https://raw.githubusercontent.com/espressif/esptool/v$EsptoolVersion/LICENSE"
$licPath = Join-Path $toolsOut 'LICENSE.txt'
try { Invoke-WebRequest -Uri $licUrl -OutFile $licPath -UseBasicParsing }
catch { Fail "Could not download the esptool license from $licUrl." }

@"
esptool v$EsptoolVersion - Copyright (C) Espressif Systems and contributors
Licensed under the GNU General Public License v2.0 (see LICENSE.txt).
Distributed unmodified as a separate program, called by BusyLightStatus.
Source code: https://github.com/espressif/esptool/tree/v$EsptoolVersion
"@ | Set-Content -Path (Join-Path $toolsOut 'SOURCE.txt') -Encoding utf8

# ---------------------------------------------------------------- 3. firmware
Write-Step "Firmware package"
$manifestSrc = Join-Path $FirmwareDir 'manifest.json'
if (-not (Test-Path $manifestSrc)) { Fail "manifest.json not found in $FirmwareDir. Run build-firmware.ps1 first." }

$manifest = Get-Content $manifestSrc -Raw | ConvertFrom-Json
$binSrc   = Join-Path $FirmwareDir $manifest.file
if (-not (Test-Path $binSrc)) { Fail "Firmware file $($manifest.file) not found." }

$hash = (Get-FileHash $binSrc -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -ne $manifest.sha256) { Fail "SHA-256 mismatch for $($manifest.file). Rebuild the firmware." }
if (-not $manifest.usbIds -or ($manifest.usbIds -contains 'VID:PID')) { Fail "usbIds in manifest.json is empty or a placeholder." }

Copy-Item $binSrc, $manifestSrc -Destination $firmwareOut -Force
Write-Host "Firmware $($manifest.version) OK (SHA-256 verified)"

# ---------------------------------------------------------------- summary
Write-Host "`nInstaller assets ready in $OutputDir" -ForegroundColor Green
Get-ChildItem $OutputDir -Recurse -File |
    Select-Object @{n='File';e={$_.FullName.Substring($OutputDir.Length + 1)}}, @{n='KB';e={[math]::Round($_.Length / 1KB, 1)}} |
    Format-Table -AutoSize
Write-Host "Next: set AppVersion=$($manifest.version) in BusyLightStatus.iss and compile the installer."
