using System.Globalization;
using System.Text.RegularExpressions;
using VideoCutter.Core.Models;

namespace VideoCutter.Core.Services;

public record DetectionOutcome(List<CutCandidate> Candidates, List<string> Warnings);

/// <summary>
/// Runs the three ffmpeg detector passes (black / silence / scene) used by the proven
/// manual workflow, parses their stderr output, and merges nearby hits into candidates.
/// </summary>
public class DetectionService
{
    private static readonly Regex BlackRegex = new(@"black_start:(?<t>[0-9.]+)", RegexOptions.Compiled);
    private static readonly Regex SilenceStartRegex = new(@"silence_start:\s*(?<t>[0-9.]+)", RegexOptions.Compiled);
    private static readonly Regex SceneRegex = new(@"pts_time:(?<t>[0-9.]+)", RegexOptions.Compiled);

    public async Task<DetectionOutcome> DetectAsync(
        string sourcePath,
        double durationSeconds,
        bool sourceHasAudio,
        DetectionThresholds thresholds,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var tagged = new List<(double Time, CandidateSource Source)>();

        progress?.Report("Detecting black frames...");
        var blackArgs = $"-an -vf \"blackdetect=d={F(thresholds.BlackMinDuration)}:pic_th={F(thresholds.BlackPictureThreshold)}\"";
        foreach (var t in await RunDetectAsync(sourcePath, blackArgs, BlackRegex, ct))
            tagged.Add((t, CandidateSource.Black));

        if (sourceHasAudio)
        {
            progress?.Report("Detecting silence...");
            var silenceArgs = $"-vn -af \"silencedetect=n={F(thresholds.SilenceThresholdDb)}dB:d={F(thresholds.SilenceMinDuration)}\"";
            foreach (var t in await RunDetectAsync(sourcePath, silenceArgs, SilenceStartRegex, ct))
                tagged.Add((t, CandidateSource.Silence));
        }
        else
        {
            warnings.Add("Source has no audio track — silence detection skipped; relying on black-frame and scene-change detection only.");
        }

        progress?.Report("Detecting scene changes...");
        var sceneArgs = $"-an -vf \"select='gt(scene,{F(thresholds.SceneThreshold)})',showinfo\"";
        foreach (var t in await RunDetectAsync(sourcePath, sceneArgs, SceneRegex, ct))
            tagged.Add((t, CandidateSource.Scene));

        var merged = MergeCandidates(tagged, thresholds.MergeWindowSeconds)
            .Where(c => durationSeconds - c.TimeSeconds > thresholds.EndTrimSeconds)
            .OrderBy(c => c.TimeSeconds)
            .ToList();

        return new DetectionOutcome(merged, warnings);
    }

    private static List<CutCandidate> MergeCandidates(List<(double Time, CandidateSource Source)> tagged, double window)
    {
        var result = new List<CutCandidate>();
        foreach (var (time, source) in tagged.OrderBy(t => t.Time))
        {
            var existing = result.FirstOrDefault(c => Math.Abs(c.TimeSeconds - time) <= window);
            if (existing != null)
            {
                if (!existing.Sources.Contains(source)) existing.Sources.Add(source);
                existing.TimeSeconds = Math.Min(existing.TimeSeconds, time);
            }
            else
            {
                result.Add(new CutCandidate { TimeSeconds = time, Sources = new List<CandidateSource> { source } });
            }
        }
        return result;
    }

    private static async Task<List<double>> RunDetectAsync(string sourcePath, string filterArgs, Regex regex, CancellationToken ct)
    {
        var args = $"-hide_banner -nostats -i \"{sourcePath}\" {filterArgs} -f null -";
        var (_, stderr, _) = await ProcessRunner.RunAsync(FfmpegLocator.FfmpegPath, args, ct);

        var times = new List<double>();
        foreach (Match m in regex.Matches(stderr))
        {
            if (double.TryParse(m.Groups["t"].Value, CultureInfo.InvariantCulture, out var t))
                times.Add(t);
        }
        return times;
    }

    private static string F(double v) => v.ToString(CultureInfo.InvariantCulture);
}
