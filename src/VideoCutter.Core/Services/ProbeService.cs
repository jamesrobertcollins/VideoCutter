using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace VideoCutter.Core.Services;

public record ProbeResult(double DurationSeconds, bool HasAudio, bool HasVideo);

/// <summary>Wraps ffprobe for duration/stream checks.</summary>
public class ProbeService
{
    public async Task<ProbeResult> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        var args = $"-v error -show_entries format=duration:stream=codec_type -of json \"{filePath}\"";
        var (stdout, _, _) = await ProcessRunner.RunAsync(FfmpegLocator.FfprobePath, args, ct);

        double duration = 0;
        bool hasAudio = false, hasVideo = false;
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            if (root.TryGetProperty("format", out var fmt) && fmt.TryGetProperty("duration", out var durEl))
                double.TryParse(durEl.GetString(), CultureInfo.InvariantCulture, out duration);

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var s in streams.EnumerateArray())
                {
                    var type = s.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
                    if (type == "audio") hasAudio = true;
                    if (type == "video") hasVideo = true;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed/empty probe output (e.g. corrupt file) — caller sees zeroed-out result.
        }

        return new ProbeResult(duration, hasAudio, hasVideo);
    }
}

/// <summary>Shared helper for launching ffmpeg/ffprobe and capturing their output.</summary>
internal static class ProcessRunner
{
    public static async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(string exe, string args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {exe}");
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await p.WaitForExitAsync(ct);
        return (await stdoutTask, await stderrTask, p.ExitCode);
    }
}
