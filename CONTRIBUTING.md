# Contributing to VideoCutter

## Setup

```
git clone https://github.com/jamesrobertcollins/VideoCutter.git
cd VideoCutter
powershell -ExecutionPolicy Bypass -File tools/Get-Ffmpeg.ps1   # fetches ffmpeg/ffprobe once
dotnet build VideoCutter.slnx
```

Requires the .NET 10 SDK and PowerShell. See `README.md` for the full first-time-setup notes
(ffmpeg isn't committed to git — it's ~140MB over GitHub's 100MB file limit — so the fetch script
pulls it from a local cache or downloads it).

## Building & testing

- `dotnet build VideoCutter.slnx` — builds all three projects.
- `dotnet run --project src/VideoCutter.App/VideoCutter.App.csproj` — runs the GUI.
- `dotnet run --project src/VideoCutter.SmokeTest -- path\to\video.mp4` — runs the full
  detect -> export -> verify pipeline headlessly, useful for sanity-checking a detection/export
  change without going through the UI.
- If a stale `VideoCutter.exe` is still running, rebuilding the App project will fail to
  overwrite its locked DLLs — `taskkill /F /IM VideoCutter.exe` first.

## Keeping documentation in sync

**Every change that affects what the app does, how it looks, or how it's built must update the
relevant doc in the same commit (or the next one, immediately after) — not as a separate later
cleanup pass.** Docs that lag behind the code are worse than no docs, because they actively
mislead. Concretely:

| You changed... | Update... |
|---|---|
| Any user-visible behavior (new feature, UI control, keyboard shortcut, menu item, theme, export option) | `docs/USER-MANUAL.md`, and add a line under `CHANGELOG.md`'s current date |
| Internal architecture, a service's responsibility, the detection/export pipeline, why a non-obvious technical choice was made | `docs/ARCHITECTURE.md` |
| A new third-party dependency (NuGet package, bundled binary) | `THIRD-PARTY-NOTICES.md` — check its license before adding it, not after (see the licensing reasoning there: GPL vs LGPL vs MIT have very different implications for whether VideoCutter's own code stays MIT) |
| Build/setup steps, requirements, versioning scheme | `README.md` and/or this file |
| Detection thresholds or their rationale | `docs/ARCHITECTURE.md`'s "Detection pipeline" section |

When in doubt, over-document rather than skip it — a short, slightly-redundant note is cheap; a
silently-stale manual costs someone real debugging time later.

## Versioning

Don't hand-edit version numbers. `Directory.Build.props` holds `VersionMajor`/`VersionMinor` —
edit those directly only when asked to. The patch number is computed automatically from the git
commit count at build time; it is not meant to be edited and will not match reality if you do.
**Bump `VersionMajor` only on an explicit instruction to do a major-overhaul bump** — it is not
tied to any automatic feature/commit threshold.

## Licensing

VideoCutter's own code is MIT. Before adding a new dependency, check its license and update
`THIRD-PARTY-NOTICES.md` accordingly:

- **MIT/Apache/BSD**: straightforward, just list it.
- **LGPL**: fine as long as it's dynamically linked (an ordinary NuGet package reference is
  dynamic linking) and unmodified — add it to the notices with a link to its source.
- **GPL**: only acceptable if invoked as a separate process (subprocess, not linked into
  VideoCutter.exe) — see the ffmpeg entry in `THIRD-PARTY-NOTICES.md` for the exact reasoning.
  Don't add a GPL library as an in-process dependency (P/Invoke bindings, a C library linked via
  FFI, etc.) without raising it as a licensing question first — that could force the whole app to
  GPL.

## Code style / conventions

- `VideoCutter.Core` has no WPF/UI references — keep it that way so it could back a CLI or a
  different UI later without changes.
- New theme-aware controls: check whether the control's *default* WPF template actually honors
  `TemplateBinding Background`/`Foreground` before assuming a simple `<Setter>` is enough — several
  controls (ComboBox, MenuItem, ProgressBar) silently didn't, and needed full custom
  `ControlTemplate`s in `App.xaml`. See `docs/ARCHITECTURE.md`'s Theming section.
- Anything that can throw during file I/O, ffmpeg process launch, or JSON deserialization should
  catch internally and log via `Log` (`VideoCutter.Core/Services/Log.cs`) rather than let the
  exception reach the UI thread — see `MainViewModel`'s `Faulted` event pattern.
