# Changelog

All notable changes to VideoCutter are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/). Version numbers are
`Major.Minor.<git-commit-count>` (see `README.md` > Versioning) — the exact version is burned
into the exe and shown in Help > About, so it's more precise than these grouped entries; this
file groups changes by *topic* across commits for human readability, not 1:1 by version number.

## Unreleased / in progress

- Nothing currently in flight.

## 2026-11-20

### Added
- Initial scaffold: ffmpeg/ffprobe-backed black/silence/scene-change cut detection, WPF review
  UI (thumbnail list + timeline), re-encode/stream-copy export, post-export verification.
- Scrubber with live timestamp, red playhead line on the timeline, right-click-to-remove on
  timeline markers, "detection finished" popup.
- Robust logging (`%LOCALAPPDATA%\VideoCutter\logs\`) and crash hardening (global exception
  handlers, graceful fallback for a corrupted `.videocutter.json`, friendly ffmpeg-missing error).
- Branding: app icon, About box, MIT `LICENSE`.
- Theming: Light, Dark, Cyberpunk, Unix, Pink, Film, Win9x — including the real Windows titlebar
  color via DWM, persisted across restarts.
- Larger (160x90) thumbnails, rendered from a 480px-wide source scale.
- Playback speed control (0.5x-4x).
- Keyboard seeking: Left/Right = 1s, Ctrl = 5s, Alt = 10s.
- Video-Right / Video-Top layout toggle (one `Grid`, cells swap via code).
- Menu bar (File > Open/Exit; View > Layout/Theme/View Log; Help > About), replacing a cluttered
  toolbar.
- Fully custom WPF templates for ComboBox, Menu/MenuItem, and ProgressBar — the default WPF chrome
  for these controls ignores simple Background/Foreground setters, so themes other than Light
  stayed illegible/untinted until this was fixed.
- Dynamic versioning: Major.Minor from `Directory.Build.props` (manual), patch = live git commit
  count (automatic, via an MSBuild target that shells to `git rev-list --count HEAD`).
- Third-party license compliance: `THIRD-PARTY-NOTICES.md`, bundled GPLv3 (ffmpeg) and LGPL v2.1
  (libvlc/LibVLCSharp) license texts, "Third-Party Licenses" button in the About box.

### Changed
- Detection thresholds retuned against published ad-break timing research (scene threshold
  0.35->0.5, silence threshold -35dB->-45dB, new minimum-segment-length filter at 8s) — the
  original defaults produced far too many false-positive candidates.
- Progress bar moved to the lower-right of the status bar.
- "Win95/98" theme renamed to "Win9x" (display label only; saved preferences still load).

### Fixed
- `ProjectStore.LoadAsync` no longer throws on a corrupted/schema-mismatched `.videocutter.json`
  (was the root cause of an app-crash-on-load report) — now logs and falls back to a fresh
  detection pass.
- Removed a confusing "(while not typing in a text box)" parenthetical from the keyboard-shortcut
  hint text.
- Top-level menu bar (File/View/Help) was far more spread out than a normal Windows app — the
  custom MenuItem template's checkmark/gesture-text/submenu-arrow gutter columns (meant for
  submenu items) were also being reserved on top-level headers. Now zeroed for top-level items.
- Every menu dropdown and ComboBox popup (Theme/Speed) showed an always-visible scrollbar even
  with only a couple of items — `ScrollViewer`'s default `VerticalScrollBarVisibility` is
  `Visible`, not `Auto`. Fixed in both custom templates (one fix covers every menu/submenu).
