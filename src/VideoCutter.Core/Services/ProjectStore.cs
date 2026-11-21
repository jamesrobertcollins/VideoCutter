using System.Text.Json;
using VideoCutter.Core.Models;

namespace VideoCutter.Core.Services;

/// <summary>Autosaves/loads review state next to the source as "{name}.videocutter.json".</summary>
public static class ProjectStore
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string GetProjectPath(string sourcePath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, Path.GetFileNameWithoutExtension(sourcePath) + ".videocutter.json");

    public static async Task SaveAsync(ProjectState state, CancellationToken ct = default)
    {
        var path = GetProjectPath(state.SourcePath);
        try
        {
            state.LastSavedUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(state, Opts);
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch (Exception ex)
        {
            // Autosave failing (e.g. read-only folder, disk full) should never crash the app —
            // the user just loses crash-resilience for this session, not the session itself.
            Log.Error($"Autosave failed for '{path}'", ex);
        }
    }

    /// <summary>
    /// Returns null (never throws) if no project file exists OR if it exists but is unreadable —
    /// e.g. corrupted by a previous crash mid-write, or from an older/incompatible schema. The
    /// caller falls back to a fresh detection pass rather than taking down the app on open.
    /// </summary>
    public static async Task<ProjectState?> LoadAsync(string sourcePath, CancellationToken ct = default)
    {
        var path = GetProjectPath(sourcePath);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var state = JsonSerializer.Deserialize<ProjectState>(json, Opts);
            if (state == null)
                Log.Warn($"Project file '{path}' deserialized to null; treating as absent.");
            return state;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load project file '{path}' — treating as absent and starting fresh detection", ex);
            return null;
        }
    }
}
