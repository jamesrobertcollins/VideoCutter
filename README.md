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
   A popup confirms when detection has finished, so you know when it's safe to interact.
2. Review the candidate list on the left: thumbnail, timecode, which detector(s) flagged
   it (Black / Silence / Scene / Manual). Candidates confirmed by **two or more**
   detectors are pre-approved (checked) as the "best guess"; single-signal hits — mostly
   scene-cuts, which also fire inside a single ad on quick edits — start unchecked for you
   to review. Thresholds are tuned against published ad-break measurements (see below) to
   avoid flooding the list with false positives.
3. Tick/untick candidates, drag timeline markers to nudge them, click a thumbnail/marker
   to preview around it, or click empty timeline space to add a missed cut. Right-click a
   timeline marker to remove it instantly (handy for an accidental click). Edit the
   timecode text box directly for an exact position.
4. Use the **scrubber** under the video preview to play/seek frame-by-frame; the current
   timestamp is shown next to it, and **Add Cut Here** drops a manual cut at the exact
   playhead position. A **red line on the timeline** always shows where playback currently
   is, so you can see how it lines up against the candidate markers.
5. Pick **Re-encode** (frame-accurate, default) or **Stream copy** (fast, keyframe-snapped)
   and hit **Export Approved Cuts**. Clips land next to the source as
   `OriginalName_00.mp4`, `_01.mp4`, ... and are auto-verified (duration, audio presence,
   durations sum to the source) with pass/fail shown below.
6. Progress autosaves every 5s to `OriginalName.videocutter.json` next to the source, so a
   crash never loses your ticks/edits.

Naming never overwrites existing clips — re-running on a source whose `_00`/`_01`/... files
already exist starts numbering from the next free index instead.

## Detection tuning

Defaults were retuned after the first pass produced far too many candidates. Grounding:
an academic survey of 20+ real broadcast ad-breaks found the black/silent delimiter between
individual spots averages ~10 frames (~0.3-0.4s) and that spots essentially never run under
~10s; ffmpeg's own scene-detection guidance is explicit that too many false positives means
the threshold is too low. Changes from the original pass:

- **Scene threshold** 0.35 → 0.5 (the original fired on ordinary quick cuts *inside* a spot).
- **Silence threshold** -35dB → -45dB (ordinary quiet dialogue was being caught as "silence").
- **New: minimum segment length** (default 8s) — any candidate that would carve out a segment
  shorter than this is dropped in favor of its stronger (more-detector) neighbor, since no
  real spot runs that short.

All of these are exposed in the Advanced panel if a particular source needs different values.

## Logging

Every session writes to `%LOCALAPPDATA%\VideoCutter\logs\videocutter-{date}.log` — probing,
every detector pass and hit count, export/verification steps, every ffmpeg/ffprobe command
line, and the full exception + stack trace for anything that goes wrong. **View > View Log**
opens today's file directly in Explorer.

The app is also hardened against the exception propagating into a crash in the first place:
- `MainViewModel`'s file-open/detect/export methods catch internally, log, and surface a
  `Faulted` event the window turns into an error dialog — the app stays open and usable.
- A corrupted or schema-mismatched `.videocutter.json` (e.g. left over from a crash mid-write)
  is logged and treated as absent, falling back to a fresh detection pass, instead of throwing.
- ffmpeg/ffprobe failing to launch (most likely cause: not actually bundled — see "First-time
  setup" above) raises a clear "could not launch ffmpeg, check PATH/bundling" error instead of
  a bare Win32 "cannot find the file specified".
- App-level `DispatcherUnhandledException` / `AppDomain.UnhandledException` /
  `TaskScheduler.UnobservedTaskException` handlers log anything that slips past the above, so a
  crash always leaves a log entry even if the process still has to go down.

## Branding, themes & UI

- **About / license**: Help > About VideoCutter shows the version, credits, and the MIT
  license (also viewable directly via its **View License** button, or `LICENSE` at the repo root).
- **Themes**: View > Theme switches between Light, Dark, Cyberpunk, Unix, Pink, Film (filmstrip
  background art), and Win95/98 — each recolors the whole UI *and* the real Windows titlebar via
  DWM (`DWMWA_CAPTION_COLOR`/`DWMWA_USE_IMMERSIVE_DARK_MODE`, Windows 11 22000+; older Windows
  just keeps the default chrome). The choice persists to
  `%LOCALAPPDATA%\VideoCutter\settings.json` across restarts. Every control (Button, TextBox,
  ComboBox, Menu, etc.) uses a fully custom-templated style in `App.xaml` so theme colors
  actually apply — the default WPF chrome for ComboBox/Menu otherwise ignores simple
  Background/Foreground setters and stays illegible on dark themes.
- **Layout**: View > Layout toggles the review pane between video-on-the-right (default; the
  video gets the larger 3-of-5 share, candidate list the narrower 2-of-5) and video-on-top with
  the candidate list filling the width below it. One `Grid` is reused for both arrangements —
  `MainWindow.xaml.cs`'s `ApplyReviewLayout` just moves the same controls between cells.
- **Playback**: a Speed dropdown (0.5x-4x) next to Play/Pause/Stop controls `MediaPlayer.SetRate`
  for faster review. Keyboard seeking works whenever focus isn't in a text box: Left/Right = 1s,
  Ctrl+Left/Right = 5s, Alt+Left/Right = 10s.
- **Thumbnails** render at 160x90 in the list (generated at 480px source width for crispness).

## Versioning

`Directory.Build.props` holds `VersionMajor`/`VersionMinor` (currently 1.0) — edit these by hand,
and bump `VersionMajor` **only** on an explicit "major overhaul" instruction. The patch number is
*not* manual: a build target in `VideoCutter.App.csproj` shells out to
`git rev-list --count HEAD` and uses that commit count as the patch number automatically, so the
version visible in About / the exe's file properties always advances with every commit with zero
upkeep (falls back to `0` if git/the repo isn't available, e.g. a zip export with no `.git`).

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
