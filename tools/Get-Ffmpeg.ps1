param(
    [string]$DestDir = (Join-Path $PSScriptRoot "ffmpeg")
)

$exe = Join-Path $DestDir "ffmpeg.exe"
$probeExe = Join-Path $DestDir "ffprobe.exe"
$licenseFile = Join-Path $DestDir "LICENSE.txt"

if ((Test-Path $exe) -and (Test-Path $probeExe) -and (Test-Path $licenseFile)) {
    Write-Host "ffmpeg/ffprobe already present in $DestDir"
    exit 0
}

New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# NOTE ON LICENSING: gyan.dev's essentials/full Windows builds (and the one cached below) are
# always GPLv3 - they bundle libx264/libx265, which VideoCutter's re-encode export mode depends
# on, so an LGPL-only build (no libx264) is not usable here. Because ffmpeg.exe/ffprobe.exe are
# invoked as separate OS processes (never linked into VideoCutter.exe), VideoCutter's own code is
# unaffected and stays MIT - but the GPLv3 text (fetched alongside the binaries below) must ship
# with every distribution of these binaries. See THIRD-PARTY-NOTICES.md at the repo root.

# 1) Fast path: this dev machine already has a cached Hermes ffmpeg build.
$localCacheRoot = Join-Path $env:LOCALAPPDATA "hermes\tools\ffmpeg-9.0.1-win32-x64"
$localCache = Join-Path $localCacheRoot "bin"
if ((Test-Path (Join-Path $localCache "ffmpeg.exe")) -and (Test-Path (Join-Path $localCache "ffprobe.exe"))) {
    Copy-Item (Join-Path $localCache "ffmpeg.exe") $exe -Force
    Copy-Item (Join-Path $localCache "ffprobe.exe") $probeExe -Force
    $localLicense = Join-Path $localCacheRoot "LICENSE.txt"
    if (Test-Path $localLicense) {
        Copy-Item $localLicense $licenseFile -Force
    } else {
        Write-Warning "No LICENSE.txt found alongside the cached ffmpeg build - fetching GPLv3 text directly."
        Invoke-WebRequest -Uri "https://www.gnu.org/licenses/gpl-3.0.txt" -OutFile $licenseFile -UseBasicParsing
    }
    Write-Host "Copied ffmpeg/ffprobe/LICENSE.txt from local Hermes cache: $localCacheRoot"
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
$licenseBin = Get-ChildItem -Path $extractDir -Recurse -Filter "LICENSE.txt" | Select-Object -First 1

if (-not $ffmpegBin -or -not $ffprobeBin) {
    Write-Error "Could not locate ffmpeg.exe/ffprobe.exe inside the downloaded archive."
    exit 1
}

Copy-Item $ffmpegBin.FullName $exe -Force
Copy-Item $ffprobeBin.FullName $probeExe -Force
if ($licenseBin) {
    Copy-Item $licenseBin.FullName $licenseFile -Force
} else {
    Write-Warning "No LICENSE.txt found inside the downloaded archive - fetching GPLv3 text directly."
    Invoke-WebRequest -Uri "https://www.gnu.org/licenses/gpl-3.0.txt" -OutFile $licenseFile -UseBasicParsing
}
Write-Host "Downloaded and installed ffmpeg/ffprobe/LICENSE.txt to $DestDir"
