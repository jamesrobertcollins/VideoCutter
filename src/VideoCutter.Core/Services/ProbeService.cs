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
        Log.Info($"Probing '{filePath}'");
        try
        {
            if (!File.Exists(filePath))
            {
                Log.Error($"Probe failed: file does not exist: '{filePath}'");
                return new ProbeResult(0, false, false);
            }

            var args = $"-v error -show_entries format=duration:stream=codec_type -of json \"{filePath}\"";
            var (stdout, stderr, exitCode) = await ProcessRunner.RunAsync(FfmpegLocator.FfprobePath, args, ct);

            if (exitCode != 0)
                Log.Warn($"ffprobe exited {exitCode} for '{filePath}': {Tail(stderr, 500)}");

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
            catch (JsonException jex)
            {
                // Malformed/empty probe output (e.g. corrupt file) — caller sees zeroed-out result.
                Log.Warn($"Probe JSON parse failed for '{filePath}': {jex.Message}. Raw stdout: {Tail(stdout, 300)}");
            }

            Log.Info($"Probe result for '{filePath}': duration={duration:F2}s hasAudio={hasAudio} hasVideo={hasVideo}");
            return new ProbeResult(duration, hasAudio, hasVideo);
        }
        catch (Exception ex)
        {
            Log.Error($"Probe threw for '{filePath}'", ex);
            throw;
        }
    }

    private static string Tail(string s, int n) => s.Length <= n ? s : s[^n..];
}

/// <summary>Shared helper for launching ffmpeg/ffprobe and capturing their output.</summary>
internal static class ProcessRunner
{
    public static async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(string exe, string args, CancellationToken ct = default)
    {
        Log.Process(exe, args);
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process p;
        try
        {
            p = Process.Start(psi) ?? throw new InvalidOperationException($"Process.Start returned null for '{exe}'.");
        }
        catch (System.ComponentModel.Win32Exception winEx)
        {
            // The #1 likely real-world cause of a crash-on-open: the bundled/PATH ffmpeg isn't
            // actually there. Wrap with a message that says exactly that, instead of a bare
            // "cannot find the file specified" that gives no clue where to look.
            Log.Error($"Could not start '{exe}' (args: {args}). Win32 error: {winEx.Message}. " +
                      "Most likely cause: ffmpeg/ffprobe weren't bundled into the build output " +
                      "(see README — run tools/Get-Ffmpeg.ps1 then rebuild) or aren't on PATH.", winEx);
            throw new InvalidOperationException(
                $"Could not launch '{exe}'. It isn't bundled next to the app and isn't on PATH. " +
                "Run tools/Get-Ffmpeg.ps1 and rebuild — see the log for details.", winEx);
        }

        using (p)
        {
            var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = p.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(stdoutTask, stderrTask);
            await p.WaitForExitAsync(ct);
            return (await stdoutTask, await stderrTask, p.ExitCode);
        }
    }
}
