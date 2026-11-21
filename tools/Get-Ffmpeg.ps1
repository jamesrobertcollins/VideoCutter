param(
    [string]$DestDir = (Join-Path $PSScriptRoot "ffmpeg")
)

$exe = Join-Path $DestDir "ffmpeg.exe"
$probeExe = Join-Path $DestDir "ffprobe.exe"

if ((Test-Path $exe) -and (Test-Path $probeExe)) {
    Write-Host "ffmpeg/ffprobe already present in $DestDir"
    exit 0
}

New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# 1) Fast path: this dev machine already has a cached Hermes ffmpeg build.
$localCache = Join-Path $env:LOCALAPPDATA "hermes\tools\ffmpeg-9.0.1-win32-x64\bin"
if ((Test-Path (Join-Path $localCache "ffmpeg.exe")) -and (Test-Path (Join-Path $localCache "ffprobe.exe"))) {
    Copy-Item (Join-Path $localCache "ffmpeg.exe") $exe -Force
    Copy-Item (Join-Path $localCache "ffprobe.exe") $probeExe -Force
    Write-Host "Copied ffmpeg/ffprobe from local Hermes cache: $localCache"
    exit 0
}

# 2) Fallback: download a static Windows build so the app works on a machine with nothing preinstalled.
$url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$zip = Join-Path $env:TEMP "videocutter-ffmpeg-essentials.zip"
$extractDir = Join-Path $env:TEMP "videocutter-ffmpeg-extract"

Write-Host "Downloading ffmpeg from $url ..."
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -Path $zip -DestinationPath $extractDir -Force

$ffmpegBin = Get-ChildItem -Path $extractDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
$ffprobeBin = Get-ChildItem -Path $extractDir -Recurse -Filter "ffprobe.exe" | Select-Object -First 1

if (-not $ffmpegBin -or -not $ffprobeBin) {
    Write-Error "Could not locate ffmpeg.exe/ffprobe.exe inside the downloaded archive."
    exit 1
}

Copy-Item $ffmpegBin.FullName $exe -Force
Copy-Item $ffprobeBin.FullName $probeExe -Force
Write-Host "Downloaded and installed ffmpeg/ffprobe to $DestDir"
