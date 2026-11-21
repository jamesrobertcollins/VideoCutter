using System.Globalization;

namespace VideoCutter.Core.Services;

/// <summary>
/// Minimal, dependency-free file logger. Every call opens, appends, and closes the file so a
/// line is durable on disk even if the process crashes immediately after — no buffered writer
/// to lose on a hard crash. Thread-safe via a single lock; logging itself never throws outward.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _logPath;

    /// <summary>Full path of today's log file (created on first use).</summary>
    public static string LogPath
    {
        get
        {
            if (_logPath != null) return _logPath;
            lock (Gate)
            {
                if (_logPath != null) return _logPath;
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VideoCutter", "logs");
                try { Directory.CreateDirectory(dir); } catch { /* fall through to best-effort write */ }
                _logPath = Path.Combine(dir, $"videocutter-{DateTime.Now:yyyy-MM-dd}.log");
                return _logPath;
            }
        }
    }

    public static string LogDirectory => Path.GetDirectoryName(LogPath) ?? AppContext.BaseDirectory;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) =>
        Write("ERROR", $"{message} :: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    /// <summary>Logs an external process invocation (ffmpeg/ffprobe) at a low-noise level.</summary>
    public static void Process(string exe, string args) => Write("PROC", $"{exe} {args}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level}] {message}";
        try
        {
            lock (Gate)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never be the thing that crashes the app.
        }

        try { System.Diagnostics.Debug.WriteLine(line); } catch { /* ignore */ }
    }

    /// <summary>Returns the last N lines of today's log, for quick on-screen diagnostics.</summary>
    public static List<string> TailLines(int count = 200)
    {
        try
        {
            lock (Gate)
            {
                if (!File.Exists(LogPath)) return new List<string>();
                var all = File.ReadAllLines(LogPath);
                return all.Length <= count ? all.ToList() : all[^count..].ToList();
            }
        }
        catch
        {
            return new List<string>();
        }
    }
}
