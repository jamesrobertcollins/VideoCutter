# Third-Party Notices

VideoCutter's own source code is licensed under the MIT License (see `LICENSE` at the repo
root). It bundles or depends on the third-party components below, under their own licenses.
Full license texts are in `licenses/` and `tools/ffmpeg/LICENSE.txt`.

## FFmpeg / ffprobe — GPLv3

- **What**: `tools/ffmpeg/ffmpeg.exe` and `ffmpeg.exe`/`ffprobe.exe` bundled with the app
  (fetched by `tools/Get-Ffmpeg.ps1`, not committed to git — see that script and `.gitignore`).
- **Version**: gyan.dev Windows build, `n9.0.1-11-ge47273f4d9-20260831` (run `ffmpeg -version` to
  confirm what's actually installed; the fetch script always pulls `ffmpeg-release-essentials`
  from <https://www.gyan.dev/ffmpeg/builds/> if no local copy is cached).
- **License**: GNU General Public License v3 (full text: `licenses/GPL-3.0.txt`, and a copy ships
  alongside the binaries themselves at `tools/ffmpeg/LICENSE.txt`). gyan.dev's Windows builds are
  **always GPLv3**, never LGPL, because they're built with `--enable-gpl` and include GPL-only
  codecs (`libx264`, `libx265`, `libxvid`) — VideoCutter's "Re-encode" export mode specifically
  needs `libx264`, so an LGPL-only ffmpeg build (no libx264) isn't usable here regardless.
- **Source**: <https://ffmpeg.org/download.html> (upstream source) and
  <https://www.gyan.dev/ffmpeg/builds/> (this specific Windows build, including its exact build
  script/configuration). FFmpeg's own source is unmodified by this project.
- **How VideoCutter uses it**: `ffmpeg.exe`/`ffprobe.exe` are invoked as separate OS processes
  (`System.Diagnostics.Process`, see `VideoCutter.Core/Services/ProbeService.cs`,
  `DetectionService.cs`, `ThumbnailService.cs`, `ExportService.cs`) — VideoCutter.exe never links
  against ffmpeg's libraries (no FFmpeg.AutoGen or similar). Running a separate GPL program as a
  subprocess and reading its stdout, or bundling it alongside a differently-licensed program on
  the same distribution medium, does not by itself place the calling program under the GPL (this
  is "mere aggregation" / independent communicating programs, not linking) — so VideoCutter's own
  code stays MIT. Redistributing the ffmpeg.exe/ffprobe.exe binaries themselves, however, **does**
  require passing along the GPLv3 license text and a pointer to the corresponding source, both of
  which this file and `tools/ffmpeg/LICENSE.txt` provide.

## LibVLC (native) — LGPL v2.1-or-later

- **What**: native `libvlc.dll` / `libvlccore.dll` / codec plugins, delivered via the
  `VideoLAN.LibVLC.Windows` NuGet package referenced by `VideoCutter.App.csproj`.
- **License**: GNU Lesser General Public License v2.1-or-later (full text:
  `licenses/LGPL-2.1.txt`).
- **Source**: <https://code.videolan.org/videolan/vlc> (VLC/libvlc upstream). Unmodified.
- **How VideoCutter uses it**: loaded at runtime as a separate native shared library (dynamic
  linking) via LibVLCSharp's P/Invoke layer — never statically linked, never modified. Dynamic
  linking to an LGPL library from differently-licensed code is exactly what the LGPL is designed
  to permit; VideoCutter's own code is unaffected and stays MIT. Because `libvlc.dll` ships as its
  own file, a user can already replace/relink it with a modified build, satisfying the LGPL's
  relinking requirement without any extra work on our part.

## LibVLCSharp / LibVLCSharp.WPF — LGPL v2.1

- **What**: the C# bindings (`LibVLCSharp`, `LibVLCSharp.WPF` NuGet packages) used for video
  preview/scrubbing (`VideoViewer` in `MainWindow.xaml`, `MediaPlayer` usage in
  `MainWindow.xaml.cs`).
- **License**: GNU Lesser General Public License v2.1 (full text: `licenses/LGPL-2.1.txt`).
- **Source**: <https://code.videolan.org/videolan/LibVLCSharp> (also mirrored at
  <https://github.com/videolan/libvlcsharp>). Unmodified.
- **How VideoCutter uses it**: referenced as an ordinary NuGet package dependency, unmodified,
  same dynamic-linking reasoning as libvlc above.

## CommunityToolkit.Mvvm — MIT

- **What**: `CommunityToolkit.Mvvm` NuGet package (Microsoft), used for
  `ObservableObject`/`RelayCommand` in `MainViewModel.cs`.
- **License**: MIT License — fully compatible with VideoCutter's own MIT license, no special
  obligations beyond retaining Microsoft's copyright notice, which NuGet/MSBuild already embeds
  in the published package metadata.
- **Source**: <https://github.com/CommunityToolkit/dotnet>

## Film theme background art

- `src/VideoCutter.App/Resources/filmstrip.jpg` is AI-generated artwork created for this project
  specifically (not a third-party asset), used under the same MIT license as the rest of
  VideoCutter's own code/assets.

## Summary for redistributors

If you redistribute a built copy of VideoCutter (installer, zip, etc.):

1. Keep `LICENSE` (VideoCutter's own MIT license) and this `THIRD-PARTY-NOTICES.md` file.
2. Keep `tools/ffmpeg/LICENSE.txt` (or `licenses/GPL-3.0.txt`) alongside the ffmpeg/ffprobe
   binaries — required because those binaries are GPLv3.
3. Keep `licenses/LGPL-2.1.txt` alongside the app — required because libvlc/LibVLCSharp are LGPL.
4. Don't statically link ffmpeg's or libvlc's code into VideoCutter.exe itself (this project
   never does, and doing so would change the licensing analysis above).
