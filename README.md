# VideoCutter

A small Windows desktop tool for cutting commercial compilations: it makes a best-guess
at cut points (black frame / silence / scene-change detection via ffmpeg), lets you
review each candidate as a thumbnail with play-on-demand preview, tick/drag/retime the
real ones, then exports every segment between approved cuts as its own named clip.

## Requirements

- Windows, .NET 10 SDK (`dotnet --list-sdks` should show `10.0.x`)
- PowerShell (for the ffmpeg fetch script)

## First-time setup

```
git clone <this repo> VideoCutter
cd VideoCutter
powershell -ExecutionPolicy Bypass -File tools/Get-Ffmpeg.ps1   # fetches ffmpeg/ffprobe once
dotnet build VideoCutter.slnx
```

ffmpeg/ffprobe are **not** committed to git (ffmpeg.exe alone is ~140MB, over GitHub's
100MB file limit). The fetch script copies them from a local Hermes cache if present,
otherwise downloads a static Windows build. The build also runs this script automatically
before every build — but on a brand-new clone, run it once manually first, since MSBuild
evaluates the `Content` include condition before the auto-fetch target runs, so the very
first build won't see files that didn't exist yet when the project was loaded.

## Running

```
dotnet run --project src/VideoCutter.App/VideoCutter.App.csproj
```

1. **Open Source Video...** — probes the file and runs detection automatically (or
   resumes a previous session if a `<name>.videocutter.json` project file sits next to it).
2. Review the candidate list on the left: thumbnail, timecode, which detector(s) flagged
   it (Black / Silence / Scene / Manual). Candidates confirmed by **two or more**
   detectors are pre-approved (checked) as the "best guess"; single-signal hits — mostly
   scene-cuts, which also fire inside a single ad on quick edits — start unchecked for you
   to review.
3. Tick/untick candidates, drag timeline markers to nudge them, click a thumbnail/marker
   to preview around it, or click empty timeline space to add a missed cut. Edit the
   timecode text box directly for an exact position.
4. Pick **Re-encode** (frame-accurate, default) or **Stream copy** (fast, keyframe-snapped)
   and hit **Export Approved Cuts**. Clips land next to the source as
   `OriginalName_00.mp4`, `_01.mp4`, ... and are auto-verified (duration, audio presence,
   durations sum to the source) with pass/fail shown below.
5. Progress autosaves every 5s to `OriginalName.videocutter.json` next to the source, so a
   crash never loses your ticks/edits.

Naming never overwrites existing clips — re-running on a source whose `_00`/`_01`/... files
already exist starts numbering from the next free index instead.

## Project layout

- `src/VideoCutter.Core` — detection/export/verification engine (no UI dependencies,
  shells out to the bundled ffmpeg/ffprobe).
- `src/VideoCutter.App` — WPF UI (timeline, thumbnail review list, LibVLCSharp preview).
- `src/VideoCutter.SmokeTest` — a headless console harness that runs the full
  detect → export → verify pipeline against a file, for quick sanity checks without the GUI:
  `dotnet run --project src/VideoCutter.SmokeTest -- path\to\video.mp4`
- `tools/Get-Ffmpeg.ps1` — fetches the bundled ffmpeg/ffprobe binaries.

## Status

Core detect/export/verify pipeline has been exercised end-to-end against a synthetic test
clip (3 segments separated by black+silence gaps): both real cut points were detected,
correctly tagged by multiple detectors, pre-approved, exported, and verified. The WPF UI
builds and renders; it has not yet been driven through a full manual review session against
a real multi-hour VHS-capture compilation — do that before trusting it on irreplaceable
source footage.
