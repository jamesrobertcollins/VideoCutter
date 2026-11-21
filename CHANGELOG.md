# Changelog

All notable changes to VideoCutter are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/). Version numbers are
`Major.Minor.<git-commit-count>` (see `README.md` > Versioning) — the exact version is burned
into the exe and shown in Help > About, so it's more precise than these grouped entries; this
file groups changes by *topic* across commits for human readability, not 1:1 by version number.

## Unreleased / in progress

- **Fixed**: opening a different source file while another was already loaded leaked that
  previous file's manually-added candidates into the new file's candidate list (tagged
  "Manual", clustered invisibly at the very start of the timeline — looked like detected
  candidates simply weren't showing up). Candidates now always clear at the start of opening
  any new file.
- **Export results popup**: export/verification results now show in a dedicated results window
  with a **Save List...** button (saves as `.txt` or `.csv`) instead of an inline list on the
  main window that lingered until the next file was opened.
- **Meaningful progress bar**: the progress bar is no longer a plain animation — it shows real
  completion: phase-by-phase during detection (black/silence/scene), per-clip during export
  (stream-copy mode) and verification, and for re-encode export specifically, a live percentage
  parsed directly from ffmpeg's own progress output as it encodes.
- **Busy indicator**: a small scissors icon next to the progress bar animates (snips back and
  forth) while any operation is running, as a quick visual confirmation the app hasn't frozen.
- **Resizable video preview**: the preview area is no longer a fixed 260px height — it fills
  most of the available vertical space by default and can be dragged larger/smaller via a
  handle between the video and the playback controls.
- **Thumbnail size selector**: View > Thumbnail Size now offers Extra Small / Small / Medium
  (default, matches the previous fixed size) / Large / Extra Large for the candidate list.
  Persists across restarts alongside the theme preference.

## v1.0.12 — 2026-11-21

First published Windows binary release:
[GitHub Releases](https://github.com/jamesrobertcollins/VideoCutter/releases/tag/v1.0.12) —
self-contained win-x64 build (~260MB, includes the .NET runtime and bundled ffmpeg so no
separate install is needed).

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
