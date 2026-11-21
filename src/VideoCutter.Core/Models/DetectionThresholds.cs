namespace VideoCutter.Core.Models;

/// <summary>
/// Tunable parameters for the detection pass.
///
/// Defaults are retuned from the original (too-noisy) pass against published measurements of
/// real ad breaks: an academic survey of 20+ broadcast ad-breaks (DCU, Shamaie/Sadlier) found
/// the black/silent delimiter between individual spots averages ~10 frames (~0.3-0.4s, shortest
/// observed ~8 frames) and that individual spots average ~25s with essentially none under ~10s.
/// Editing-industry sources agree ad-break black/silence gaps run from a fraction of a second to
/// a few seconds depending on network, and many modern hard cuts have none at all — so black and
/// silence detection are inherently conservative (they only ever fire on genuine gaps). The actual
/// source of "way too many cuts" is the scene-change detector, which also fires on ordinary quick
/// cuts *inside* a single spot; ffmpeg's own tuning guidance for that filter is explicit: too many
/// false positives means the threshold is too low, so it's raised here from 0.35 to 0.5. A new
/// MinSegmentSeconds floor also throws out any candidate that would carve out an implausibly short
/// segment, since no real spot is anywhere near that short.
/// </summary>
public class DetectionThresholds
{
    public double BlackMinDuration { get; set; } = 0.4;
    public double BlackPictureThreshold { get; set; } = 0.98;

    /// <summary>Stricter than the original -35dB: ordinary quiet dialogue was being caught as "silence".</summary>
    public double SilenceThresholdDb { get; set; } = -45;
    public double SilenceMinDuration { get; set; } = 0.4;

    /// <summary>Raised from 0.35: the original value fired on ordinary quick cuts inside a single spot.</summary>
    public double SceneThreshold { get; set; } = 0.5;

    /// <summary>Candidates within this many seconds of each other are merged into one.</summary>
    public double MergeWindowSeconds { get; set; } = 1.0;

    /// <summary>Candidates within this many seconds of end-of-file are dropped (thumbnail grab fails there).</summary>
    public double EndTrimSeconds { get; set; } = 1.0;

    /// <summary>
    /// Minimum plausible segment length. Real ad spots run ~10-30s+; a candidate that would carve
    /// out a shorter segment than this is dropped (or, if it has stronger multi-detector evidence
    /// than its too-close neighbor, replaces it instead of just adding to the pile).
    /// </summary>
    public double MinSegmentSeconds { get; set; } = 8.0;

    public DetectionThresholds Clone() => new()
    {
        BlackMinDuration = BlackMinDuration,
        BlackPictureThreshold = BlackPictureThreshold,
        SilenceThresholdDb = SilenceThresholdDb,
        SilenceMinDuration = SilenceMinDuration,
        SceneThreshold = SceneThreshold,
        MergeWindowSeconds = MergeWindowSeconds,
        EndTrimSeconds = EndTrimSeconds,
        MinSegmentSeconds = MinSegmentSeconds,
    };
}
