using System.Globalization;
using System.Text.RegularExpressions;
using VideoCutter.Core.Models;

namespace VideoCutter.Core.Services;

/// <summary>
/// Turns a set of approved cut times into named segment plans and renders them with ffmpeg,
/// either frame-accurate (re-encode) or fast (stream copy, keyframe-snapped).
/// Output always goes beside the source file; never overwrites an existing clip.
/// </summary>
public class ExportService
{
    public List<SegmentPlan> PlanSegments(string sourcePath, IEnumerable<double> approvedCutTimes, double durationSeconds)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var outputDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;

        var boundaries = new List<double> { 0 };
        boundaries.AddRange(approvedCutTimes.Where(t => t > 0 && t < durationSeconds).Distinct());
        boundaries.Add(durationSeconds);
        boundaries = boundaries.OrderBy(t => t).Distinct().ToList();

        int count = boundaries.Count - 1;
        int startIndex = FindFreeStartIndex(outputDir, baseName, ext, count);

        var plans = new List<SegmentPlan>();
        for (int i = 0; i < count; i++)
        {
            int idx = startIndex + i;
            plans.Add(new SegmentPlan
            {
                Index = idx,
                StartSeconds = boundaries[i],
                EndSeconds = boundaries[i + 1],
                FileName = $"{baseName}_{idx:D2}{ext}",
            });
        }
        return plans;
    }

    private static int FindFreeStartIndex(string dir, string baseName, string ext, int count)
    {
        for (int start = 0; ; start++)
        {
            bool anyExists = false;
            for (int i = 0; i < count; i++)
            {
                if (File.Exists(Path.Combine(dir, $"{baseName}_{start + i:D2}{ext}"))) { anyExists = true; break; }
            }
            if (!anyExists) return start;
        }
    }

    public async Task ExportAsync(
        string sourcePath,
        List<SegmentPlan> plans,
        ExportMode mode,
        IProgress<(string Message, double? Percent)>? progress = null,
        CancellationToken ct = default)
    {
        Log.Info($"Export starting for '{sourcePath}': {plans.Count} segment(s), mode={mode}");
        try
        {
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
            Directory.CreateDirectory(outputDir);

            if (mode == ExportMode.StreamCopy)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var plan = plans[i];
                    var pct = plans.Count > 0 ? 100.0 * i / plans.Count : (double?)null;
                    progress?.Report(($"Exporting {plan.FileName} (stream copy)... ({i + 1}/{plans.Count})", pct));
                    var outPath = Path.Combine(outputDir, plan.FileName);
                    var args = $"-y -ss {F(plan.StartSeconds)} -i \"{sourcePath}\" -t {F(plan.DurationSeconds)} -c copy \"{outPath}\"";
                    await RunOrThrow(args, ct);
                }
                progress?.Report(("Export finished.", 100.0));
                Log.Info($"Export finished (stream copy): {plans.Count} clip(s) written to '{outputDir}'.");
                return;
            }

            // Re-encode: single ffmpeg segment pass is far faster than N re-encodes, so we do the
            // whole file at once with ffmpeg's own sequential naming, then rename to our plan.
            // Total duration being encoded = the last plan's end time (boundaries span 0..source
            // duration), used to turn ffmpeg's live "time=" stats into a real percent-complete.
            var totalDuration = plans.Count > 0 ? plans[^1].EndSeconds : 0;
            progress?.Report(("Re-encoding and segmenting (this can take a while)...", 0.0));
            var tempDir = Path.Combine(outputDir, $".videocutter_tmp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var ext = Path.GetExtension(sourcePath);
                var pattern = Path.Combine(tempDir, "part_%03d" + ext);
                var segTimes = string.Join(",", plans.Skip(1).Select(p => F(p.StartSeconds)));
                var args = $"-y -i \"{sourcePath}\" -map 0:v:0 -map \"0:a:0?\" " +
                           $"-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k " +
                           $"-f segment -segment_times {segTimes} -reset_timestamps 1 \"{pattern}\"";

                void OnLine(string line)
                {
                    var m = FfmpegTimeRegex.Match(line);
                    if (!m.Success) return;
                    var elapsed = double.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture) * 3600
                                + double.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) * 60
                                + double.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture);
                    var pct = totalDuration > 0 ? Math.Min(99.0, elapsed / totalDuration * 100) : (double?)null;
                    progress?.Report(($"Re-encoding and segmenting... {pct:F0}%", pct));
                }

                await RunOrThrow(args, ct, OnLine);

                for (int i = 0; i < plans.Count; i++)
                {
                    var src = Path.Combine(tempDir, $"part_{i:D3}{ext}");
                    var dst = Path.Combine(outputDir, plans[i].FileName);
                    if (File.Exists(src))
                        File.Move(src, dst, overwrite: false);
                    else
                        throw new InvalidOperationException($"Expected segment output '{src}' was not produced by ffmpeg.");
                }
                progress?.Report(("Export finished.", 100.0));
                Log.Info($"Export finished (re-encode): {plans.Count} clip(s) written to '{outputDir}'.");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* best effort cleanup */ }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Export threw for '{sourcePath}'", ex);
            throw;
        }
    }

    private static readonly Regex FfmpegTimeRegex = new(@"time=(?<h>\d+):(?<m>\d+):(?<s>\d+\.\d+)", RegexOptions.Compiled);

    private static async Task RunOrThrow(string args, CancellationToken ct, Action<string>? onStderrLine = null)
    {
        var (_, stderr, exitCode) = await ProcessRunner.RunAsync(FfmpegLocator.FfmpegPath, args, ct, onStderrLine);
        if (exitCode != 0)
        {
            Log.Error($"ffmpeg export failed (exit {exitCode}): {Tail(stderr, 2000)}");
            throw new InvalidOperationException($"ffmpeg failed (exit {exitCode}): {Tail(stderr, 2000)}");
        }
    }

    private static string Tail(string s, int n) => s.Length <= n ? s : s[^n..];
    private static string F(double seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture);
}
