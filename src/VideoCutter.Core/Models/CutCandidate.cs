namespace VideoCutter.Core.Models;

public enum CandidateSource { Black, Silence, Scene, Manual }

/// <summary>A detected or user-added candidate cut point on the source timeline.</summary>
public class CutCandidate
{
    public double TimeSeconds { get; set; }

    public List<CandidateSource> Sources { get; set; } = new();

    /// <summary>Whether the user has approved this as a real cut boundary.</summary>
    public bool Approved { get; set; }

    /// <summary>Path to a generated preview thumbnail, if one has been made.</summary>
    public string? ThumbnailPath { get; set; }

    public bool IsManual => Sources.Count == 1 && Sources[0] == CandidateSource.Manual;

    public string SourceLabel => string.Join("+", Sources.Distinct());
}
