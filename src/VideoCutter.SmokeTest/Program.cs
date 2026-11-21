using VideoCutter.Core.Models;
using VideoCutter.Core.Services;

if (args.Length < 1)
{
    Console.WriteLine("Usage: VideoCutter.SmokeTest <path-to-video>");
    return 1;
}

var path = args[0];
Console.WriteLine($"=== Probing {path} ===");
var probe = new ProbeService();
var probeResult = await probe.ProbeAsync(path);
Console.WriteLine($"Duration={probeResult.DurationSeconds:F2}s HasAudio={probeResult.HasAudio} HasVideo={probeResult.HasVideo}");

Console.WriteLine("=== Detecting candidates ===");
var thresholds = new DetectionThresholds();
var detector = new DetectionService();
var progress = new Progress<string>(m => Console.WriteLine($"  [progress] {m}"));
var outcome = await detector.DetectAsync(path, probeResult.DurationSeconds, probeResult.HasAudio, thresholds, progress);

foreach (var w in outcome.Warnings) Console.WriteLine($"  [warning] {w}");
foreach (var c in outcome.Candidates)
    Console.WriteLine($"  t={c.TimeSeconds,7:F2}s  sources={string.Join("+", c.Sources)}");

foreach (var c in outcome.Candidates)
    c.Approved = c.Sources.Distinct().Count() >= 2;

var approvedTimes = outcome.Candidates.Where(c => c.Approved).Select(c => c.TimeSeconds).OrderBy(t => t).ToList();
Console.WriteLine($"=== {outcome.Candidates.Count} candidates, {approvedTimes.Count} pre-approved (best guess) ===");

Console.WriteLine("=== Planning + exporting (re-encode) ===");
var exporter = new ExportService();
var plans = exporter.PlanSegments(path, approvedTimes, probeResult.DurationSeconds);
foreach (var p in plans)
    Console.WriteLine($"  {p.FileName}: {p.StartSeconds:F2}s -> {p.EndSeconds:F2}s ({p.DurationSeconds:F2}s)");

await exporter.ExportAsync(path, plans, ExportMode.ReEncode, progress);

Console.WriteLine("=== Verifying ===");
var verifier = new VerificationService();
var outputDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
var results = await verifier.VerifyAsync(outputDir, plans, probeResult.HasAudio, probeResult.DurationSeconds);
foreach (var r in results)
    Console.WriteLine($"  {r.FileName}: passed={r.Passed} duration={r.DurationSeconds:F2}s hasAudio={r.HasAudio} msg={r.Message}");

var failCount = results.Count(r => !r.Passed);
Console.WriteLine(failCount == 0 ? "=== ALL CHECKS PASSED ===" : $"=== {failCount} CHECK(S) FAILED ===");
return failCount == 0 ? 0 : 1;
