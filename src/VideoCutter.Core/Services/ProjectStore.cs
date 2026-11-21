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
        state.LastSavedUtc = DateTime.UtcNow;
        var path = GetProjectPath(state.SourcePath);
        var json = JsonSerializer.Serialize(state, Opts);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public static async Task<ProjectState?> LoadAsync(string sourcePath, CancellationToken ct = default)
    {
        var path = GetProjectPath(sourcePath);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<ProjectState>(json, Opts);
    }
}
