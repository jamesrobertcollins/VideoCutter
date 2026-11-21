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
        var args = $"-y -ss {grabAt.ToString("0.###", CultureInfo.InvariantCulture)} -i \"{sourcePath}\" -frames:v 1 -vf scale=320:-1 \"{outPath}\"";
        await ProcessRunner.RunAsync(FfmpegLocator.FfmpegPath, args, ct);
        return outPath;
    }
}
