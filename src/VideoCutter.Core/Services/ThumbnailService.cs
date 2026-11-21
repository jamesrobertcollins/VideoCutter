using System.Globalization;

namespace VideoCutter.Core.Services;

/// <summary>Grabs a single preview frame near a candidate time for the review thumbnail strip.</summary>
public class ThumbnailService
{
    public async Task<string> GenerateAsync(string sourcePath, double atSeconds, string outputDir, string fileNameHint, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var outPath = Path.Combine(outputDir, $"{fileNameHint}.jpg");
        // Grab 0.8s after the candidate: matches the proven workflow (the candidate time itself
        // can be mid-transition; the frame just after it is a stable preview).
        var grabAt = atSeconds + 0.8;
        // 480px wide: the review list now shows thumbnails at 160x90, so this keeps them crisp.
        var args = $"-y -ss {grabAt.ToString("0.###", CultureInfo.InvariantCulture)} -i \"{sourcePath}\" -frames:v 1 -vf scale=480:-1 \"{outPath}\"";
        try
        {
            var (_, stderr, exitCode) = await ProcessRunner.RunAsync(FfmpegLocator.FfmpegPath, args, ct);
            if (exitCode != 0)
                Log.Warn($"Thumbnail grab exited {exitCode} at {atSeconds:F2}s for '{sourcePath}': {stderr}");
            return outPath;
        }
        catch (Exception ex)
        {
            Log.Error($"Thumbnail grab threw at {atSeconds:F2}s for '{sourcePath}'", ex);
            throw;
        }
    }
}
