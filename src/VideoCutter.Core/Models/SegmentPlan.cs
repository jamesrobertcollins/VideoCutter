namespace VideoCutter.Core.Models;

public enum ExportMode { ReEncode, StreamCopy }

/// <summary>One planned output clip: a time range from the source and the name it will be written under.</summary>
public class SegmentPlan
{
    public int Index { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public string FileName { get; set; } = "";

    public double DurationSeconds => EndSeconds - StartSeconds;
}

public class ClipVerification
{
    public string FileName { get; set; } = "";
    public bool Passed { get; set; }
    public string Message { get; set; } = "";
    public double DurationSeconds { get; set; }
    public bool HasAudio { get; set; }
}
