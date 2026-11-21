using System.Linq;
using VideoCutter.Core.Models;

namespace VideoCutter.Core.Services;

/// <summary>Post-export sanity checks: every clip exists, has audio if the source did, and durations add up.</summary>
public class VerificationService
{
    private readonly ProbeService _probe = new();

    public async Task<List<ClipVerification>> VerifyAsync(
        string outputDir,
        List<SegmentPlan> plans,
        bool sourceHasAudio,
        double sourceDuration,
        IProgress<(string Message, double? Percent)>? progress = null,
        CancellationToken ct = default)
    {
        Log.Info($"Verifying {plans.Count} clip(s) in '{outputDir}'");
        var results = new List<ClipVerification>();
        double sumDuration = 0;

        for (int i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            var pct = plans.Count > 0 ? 100.0 * i / plans.Count : (double?)null;
            progress?.Report(($"Verifying {plan.FileName}... ({i + 1}/{plans.Count})", pct));

            var path = Path.Combine(outputDir, plan.FileName);
            var v = new ClipVerification { FileName = plan.FileName };

            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                v.Passed = false;
                v.Message = "File missing or zero-length";
                Log.Warn($"Verification failed for '{plan.FileName}': {v.Message}");
                results.Add(v);
                continue;
            }

            var probe = await _probe.ProbeAsync(path, ct);
            v.DurationSeconds = probe.DurationSeconds;
            v.HasAudio = probe.HasAudio;
            sumDuration += probe.DurationSeconds;

            var issues = new List<string>();
            if (probe.DurationSeconds <= 0) issues.Add("zero duration");
            if (sourceHasAudio && !probe.HasAudio) issues.Add("missing audio track");

            v.Passed = issues.Count == 0;
            v.Message = issues.Count == 0 ? "OK" : string.Join("; ", issues);
            if (!v.Passed) Log.Warn($"Verification failed for '{plan.FileName}': {v.Message}");
            results.Add(v);
        }

        if (Math.Abs(sumDuration - sourceDuration) > 1.0)
        {
            var msg = $"Sum of clip durations {sumDuration:F2}s differs from source {sourceDuration:F2}s by more than 1s";
            Log.Warn(msg);
            results.Add(new ClipVerification { FileName = "(total)", Passed = false, Message = msg });
        }

        progress?.Report(("Verification finished.", 100.0));
        Log.Info($"Verification finished: {results.Count(r => r.Passed)}/{plans.Count} clip(s) passed.");
        return results;
    }
}
