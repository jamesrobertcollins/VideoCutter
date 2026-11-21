namespace VideoCutter.Core.Models;

/// <summary>Tunable parameters for the detection pass. Defaults match the proven ffmpeg workflow.</summary>
public class DetectionThresholds
{
    public double BlackMinDuration { get; set; } = 0.4;
    public double BlackPictureThreshold { get; set; } = 0.98;
    public double SilenceThresholdDb { get; set; } = -35;
    public double SilenceMinDuration { get; set; } = 0.4;
    public double SceneThreshold { get; set; } = 0.35;

    /// <summary>Candidates within this many seconds of each other are merged into one.</summary>
    public double MergeWindowSeconds { get; set; } = 1.0;

    /// <summary>Candidates within this many seconds of end-of-file are dropped (thumbnail grab fails there).</summary>
    public double EndTrimSeconds { get; set; } = 1.0;

    public DetectionThresholds Clone() => new()
    {
        BlackMinDuration = BlackMinDuration,
        BlackPictureThreshold = BlackPictureThreshold,
        SilenceThresholdDb = SilenceThresholdDb,
        SilenceMinDuration = SilenceMinDuration,
        SceneThreshold = SceneThreshold,
        MergeWindowSeconds = MergeWindowSeconds,
        EndTrimSeconds = EndTrimSeconds,
    };
}
