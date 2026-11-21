namespace VideoCutter.Core.Services;

/// <summary>
/// Finds the bundled ffmpeg/ffprobe next to the running app (tools/Get-Ffmpeg.ps1 puts them
/// under a "ffmpeg" subfolder of the build output), falling back to PATH.
/// </summary>
public static class FfmpegLocator
{
    public static string FfmpegPath => Resolve("ffmpeg.exe");
    public static string FfprobePath => Resolve("ffprobe.exe");

    private static string Resolve(string exeName)
    {
        var baseDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(baseDir, "ffmpeg", exeName);
        if (File.Exists(bundled)) return bundled;
        return exeName; // rely on PATH as a fallback
    }
}
