namespace VideoCutter.Core.Services;

/// <summary>
/// Finds the bundled ffmpeg/ffprobe next to the running app (tools/Get-Ffmpeg.ps1 puts them
/// under a "ffmpeg" subfolder of the build output), falling back to PATH.
/// </summary>
public static class FfmpegLocator
{
    private static bool _loggedOnce;

    public static string FfmpegPath => Resolve("ffmpeg.exe");
    public static string FfprobePath => Resolve("ffprobe.exe");

    private static string Resolve(string exeName)
    {
        var baseDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(baseDir, "ffmpeg", exeName);
        var found = File.Exists(bundled);

        if (!_loggedOnce)
        {
            _loggedOnce = true;
            Log.Info($"FfmpegLocator base dir: {baseDir}");
        }

        if (found) return bundled;

        Log.Warn($"Bundled {exeName} not found at '{bundled}' — falling back to PATH. " +
                 "If PATH doesn't have it either, every ffmpeg/ffprobe call will fail. " +
                 "Run tools/Get-Ffmpeg.ps1 and rebuild.");
        return exeName; // rely on PATH as a fallback
    }
}
