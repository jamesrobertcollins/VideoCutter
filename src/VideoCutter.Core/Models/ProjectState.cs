namespace VideoCutter.Core.Models;

/// <summary>
/// Full state of a review session for one source file, autosaved next to the source
/// as "{name}.videocutter.json" so a crash never loses ticks/edits.
/// </summary>
public class ProjectState
{
    public int SchemaVersion { get; set; } = 1;
    public string SourcePath { get; set; } = "";
    public double DurationSeconds { get; set; }
    public bool SourceHasAudio { get; set; } = true;
    public DetectionThresholds Thresholds { get; set; } = new();
    public List<CutCandidate> Candidates { get; set; } = new();
    public DateTime LastSavedUtc { get; set; } = DateTime.UtcNow;
}
