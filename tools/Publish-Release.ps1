# Builds a self-contained Release win-x64 publish of VideoCutter.
# Run from anywhere; always operates relative to the repo root (this script's parent dir).
#
# Usage (PowerShell): tools/Publish-Release.ps1
#
# Output: publish/VideoCutter-win-x64/ (VideoCutter.exe + bundled ffmpeg/licenses/etc.)
# See CONTRIBUTING.md "Releasing a Windows binary" for the full zip/tag/gh-release steps
# that follow this.

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir "..")
Set-Location $RepoRoot

Write-Host "==> Killing any running VideoCutter.exe (ignore 'not found' below)"
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
taskkill /F /IM VideoCutter.exe 2>&1 | Out-Null
$ErrorActionPreference = $prevEap

Write-Host "==> Cleaning previous publish output"
if (Test-Path "publish") { Remove-Item -Recurse -Force "publish" }

Write-Host "==> Publishing (Release, win-x64, self-contained)"
dotnet publish src/VideoCutter.App/VideoCutter.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -o publish/VideoCutter-win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "==> Done. Output: publish/VideoCutter-win-x64/"
Write-Host "    Sanity-check before zipping: launch publish/VideoCutter-win-x64/VideoCutter.exe once,"
Write-Host "    confirm it starts cleanly, then check %LOCALAPPDATA%/VideoCutter/logs/ for errors."
