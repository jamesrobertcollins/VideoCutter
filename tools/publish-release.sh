#!/usr/bin/env bash
# Builds a self-contained Release win-x64 publish of VideoCutter.
# Run from anywhere; always operates relative to the repo root (this script's parent dir).
#
# Usage: tools/publish-release.sh
#
# Output: publish/VideoCutter-win-x64/ (VideoCutter.exe + bundled ffmpeg/licenses/etc.)
# See CONTRIBUTING.md "Releasing a Windows binary" for the full zip/tag/gh-release steps
# that follow this.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

echo "==> Killing any running VideoCutter.exe (ignore 'not found' below)"
taskkill /F /IM VideoCutter.exe 2>&1 || true

echo "==> Cleaning previous publish output"
rm -rf publish

echo "==> Publishing (Release, win-x64, self-contained)"
dotnet publish src/VideoCutter.App/VideoCutter.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -o publish/VideoCutter-win-x64

echo
echo "==> Done. Output: publish/VideoCutter-win-x64/"
echo "    Sanity-check before zipping: launch publish/VideoCutter-win-x64/VideoCutter.exe once,"
echo "    confirm it starts cleanly, then check %LOCALAPPDATA%/VideoCutter/logs/ for errors."
