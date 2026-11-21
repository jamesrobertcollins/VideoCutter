# Architecture

A technical overview for anyone reading or modifying VideoCutter's code. For how to *use* the
app, see `docs/USER-MANUAL.md`. For how to build/contribute, see `CONTRIBUTING.md`.

## Project layout

```
VideoCutter.slnx
├── src/VideoCutter.Core/       detection/export/verification engine — no UI dependencies
│   ├── Models/                 CutCandidate, DetectionThresholds, SegmentPlan, ProjectState
│   └── Services/                ProbeService, DetectionService, ThumbnailService,
│                                ExportService, VerificationService, ProjectStore,
│                                FfmpegLocator, Log
├── src/VideoCutter.App/        WPF UI
│   ├── ViewModels/MainViewModel.cs   orchestration (detect/export/verify commands, events)
│   ├── Controls/TimelineControl.cs  custom Canvas timeline with draggable/removable markers
│   ├── Converters/                   Timecode, PathToImageSource, BoolToBrush
│   ├── Services/ThemeManager.cs      theme definitions + DWM titlebar coloring + persistence
│   ├── MainWindow.xaml(.cs)          main UI
│   ├── AboutWindow.xaml(.cs)         About box, License / Third-Party Licenses viewers
│   ├── ExportResultsWindow.xaml(.cs) export/verification results popup, Save List...
│   └── App.xaml                      app-wide themed control styles/templates
├── src/VideoCutter.SmokeTest/   headless console harness: full pipeline without the GUI
├── tools/Get-Ffmpeg.ps1         fetches ffmpeg.exe/ffprobe.exe (+ LICENSE.txt) at build time
└── licenses/                    GPL-3.0.txt, LGPL-2.1.txt (full texts)
```

`VideoCutter.Core` never references WPF or any UI type — it's a plain class library that shells
out to ffmpeg/ffprobe via `System.Diagnostics.Process` and could theoretically back a CLI or a
different UI without changes.

## Detection pipeline

1. **`ProbeService`** runs `ffprobe` once to get duration, has-audio, has-video.
2. **`DetectionService`** runs `ffmpeg` with three independent filter passes and parses stderr for
   each detector's hits:
   - **Black**: `blackdetect` (`BlackMinDuration`, `BlackPictureThreshold`).
   - **Silence**: `silencedetect` (`SilenceThresholdDb`, `SilenceMinDuration`) — skipped with a
     user-facing warning if the source has no audio track.
   - **Scene**: `select='gt(scene,N)'` (`SceneThreshold`).
3. Hits within `MergeWindowSeconds` of each other are merged into one `CutCandidate`, tagged with
   every detector that flagged it.
4. `ApplyMinSpacing()` drops any candidate that would carve out a segment shorter than
   `MinSegmentSeconds`, keeping whichever neighbor has stronger (more-detector) evidence.
5. **Best-guess auto-approval**: a candidate starts pre-checked only when **2 or more** detectors
   agree — a lone scene-cut is just as likely to be a hard cut *inside* a single ad as a real
   break, so those start unchecked for the user to review.

Current defaults (`DetectionThresholds.cs`, retuned against published ad-break timing research —
see `CHANGELOG.md`): `BlackMinDuration=0.4s`, `BlackPictureThreshold=0.98`,
`SilenceThresholdDb=-45dB`, `SilenceMinDuration=0.4s`, `SceneThreshold=0.5`,
`MergeWindowSeconds=1.0`, `EndTrimSeconds=1.0`, `MinSegmentSeconds=8s`.

## Review UI

- **`TimelineControl`**: a `Canvas`-based custom control. Click empty space to add a manual cut;
  drag a marker to nudge it; right-click to remove it. A red `PlayheadSeconds`-bound line tracks
  playback position.
- **Preview**: `LibVLCSharp` (`VideoView` + `MediaPlayer`), not WPF `MediaElement` — chosen for
  more reliable codec/seek support against VHS-capture-style sources. The scrubber `Slider` and
  the timeline's playhead both read/write `MainViewModel.CurrentTimeSeconds`.
- **Layout toggle**: `MainWindow.xaml.cs`'s `ApplyReviewLayout(bool videoOnTop)` is the only place
  that knows about both arrangements — it moves the *same* `CandidateList` / `GridSplitter` /
  `PreviewPanel` instances between a 3-column one-row grid (video-right, the default) and a
  1-column 3-row grid (video-top), rather than maintaining two parallel copies of the XAML.

## Theming

`ThemeManager.cs` is the single source of truth: `Themes` is a `Dictionary<AppTheme,
ThemeDefinition>` with every color plus optional DWM titlebar caption/text color and dark-mode
flag. `Apply(theme, window)`:

1. Overwrites `Application.Current.Resources["AppXxxBrush"]` entries — every window/control that
   references them via `DynamicResource` repaints immediately, app-wide, without reloading XAML.
2. If a `Window` is passed and already has an `HWND` (i.e. it's been shown), calls
   `DwmSetWindowAttribute` for `DWMWA_CAPTION_COLOR` / `DWMWA_TEXT_COLOR` /
   `DWMWA_USE_IMMERSIVE_DARK_MODE` (Windows 11 build 22000+; silently no-ops to default chrome on
   older Windows).
3. Persists the choice to `%LOCALAPPDATA%\VideoCutter\settings.json`.

**Why the custom ComboBox/MenuItem/ProgressBar templates in `App.xaml`**: WPF's default control
templates for these three controls hardcode parts of their visual tree against system theme
resources rather than `TemplateBinding Background`/`Foreground` — e.g. a ComboBox's dropdown
toggle button, a MenuItem's icon-gutter column, and a ProgressBar's indeterminate marquee all
ignored simple `<Setter Property="Background">` and stayed stuck looking like the OS default
(frequently illegible against a dark theme). Each of those three therefore gets a full
`ControlTemplate` replacement so every pixel is sourced from the themed `DynamicResource` brushes.
If you add a new control type and a theme other than Light makes it look wrong/illegible, this is
almost certainly why — check whether its default template actually honors `TemplateBinding` for
the property you're setting before assuming a simple `Setter` will work.

## Export & verification

- **`ExportService`**: for each approved cut, either re-encodes (`libx264 crf18 + aac 192k`,
  frame-accurate) or stream-copies (fast, snaps to the nearest keyframe) the segment between it
  and the next cut (or source start/end). Output name: `OriginalName_00.mp4`, `_01.mp4`, ...,
  auto-incrementing past any existing files with that prefix (never overwrites).
- **`VerificationService`**: after export, checks each clip has non-zero duration, has an audio
  stream if the source did, and that all clip durations sum back to roughly the source duration.
- **`ProjectStore`**: autosaves review state (every 5s) to `<name>.videocutter.json` next to the
  source; `LoadAsync` treats any deserialization failure as "no autosave" rather than throwing
  (this was the fix for a reported crash-on-load).
- **Opt-in project-file cleanup**: on a fully-passed export (`failCount == 0`),
  `MainViewModel.ExportAsync` deletes the project file if `DeleteProjectFileAfterExport` is set,
  and also sets `_suppressAutosave = true`. That second part matters: without it, the 5s autosave
  timer (`MainWindow`'s `_autosaveTimer`, which just checks `SourcePath != null`) would recreate
  the file it was just told to delete within one tick. `_suppressAutosave` is cleared on any
  genuine edit afterward (`AddCandidate`/`RemoveCandidate`/`MoveCandidate`/re-detection) or when a
  new file is opened, so autosaving resumes normally the moment there's something new worth
  saving.
- **Results popup**: `MainViewModel.ExportFinished` fires once export+verification completes;
  `MainWindow` opens an `ExportResultsWindow` with a *snapshot* (`.ToList()`, not the live
  `ObservableCollection`) of that export's results, so the popup stays frozen to the export it
  was opened for even if another export runs while it's still open.

## Progress reporting

Detection/export/verification all report `IProgress<(string Message, double? Percent)>` instead
of a bare message string — `Percent` is `null` only when no real fraction is knowable yet (e.g.
the initial probe), in which case the UI falls back to an indeterminate marquee
(`MainViewModel.IsProgressIndeterminate`); otherwise the progress bar shows the real value.

- **Detection**: phase-based (black/silence/scene are discrete ffmpeg passes) — percent jumps in
  thirds (or halves if the source has no audio and silence detection is skipped).
- **Export/verification**: per-clip (`i / total`) for stream-copy export and all of
  verification, since both iterate one file at a time.
- **Re-encode export** is the one case needing *continuous* progress, since it's a single ffmpeg
  invocation covering every segment at once. `ProcessRunner.RunAsync` gained an optional
  `onStderrLine` callback so `ExportService` can regex-match ffmpeg's own periodic `time=HH:MM:SS`
  stats lines as they stream in and compute `elapsed / totalDuration`, rather than waiting for
  the whole process to exit before reporting anything.

## Logging & crash hardening

`Log` (`VideoCutter.Core/Services/Log.cs`) is a dependency-free static logger that opens, writes,
and closes the log file on every single call — more I/O overhead than a buffered logger, but a
line written right before a crash is guaranteed to actually be on disk. `MainViewModel`'s
file-open/detect/export paths catch internally and raise a `Faulted` event rather than letting
exceptions reach the UI thread; `App.xaml.cs` additionally installs
`DispatcherUnhandledException`/`AppDomain.UnhandledException`/`TaskScheduler.UnobservedTaskException`
handlers as a last-resort backstop so a crash always leaves a log entry.

## Why ffmpeg is a subprocess, not a linked library

See `THIRD-PARTY-NOTICES.md` for the full licensing reasoning — in short: the bundled ffmpeg
build is GPLv3 (needed for `libx264`/`libx265`, which the re-encode export mode depends on).
Invoking it as a separate OS process (`System.Diagnostics.Process`) is "mere aggregation", not
linking, so it doesn't place VideoCutter's own code under the GPL. Don't switch to an in-process
binding (e.g. FFmpeg.AutoGen) against this same GPL build without re-reading that reasoning first.
