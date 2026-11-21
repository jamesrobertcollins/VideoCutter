using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoCutter.Core.Models;
using VideoCutter.Core.Services;

namespace VideoCutter.App.ViewModels;

/// <summary>
/// Owns the review session for one source file: detection, candidate editing, export and
/// verification. UI-only interactions (timeline drag, playback) are driven from the view via
/// the plain methods below rather than ICommand, since they carry extra parameters (a point in
/// time, a specific candidate) that don't map cleanly onto single-parameter commands.
/// </summary>
public class MainViewModel : ObservableObject
{
    private readonly ProbeService _probe = new();
    public readonly ThumbnailService Thumbnails = new();
    private readonly ExportService _exporter = new();
    private readonly VerificationService _verifier = new();
    private DetectionService _detector = new();

    public string? SourcePath { get; private set; }
    public string? ThumbnailDir { get; private set; }

    private double _durationSeconds;
    public double DurationSeconds { get => _durationSeconds; private set => SetProperty(ref _durationSeconds, value); }

    private bool _sourceHasAudio = true;
    public bool SourceHasAudio { get => _sourceHasAudio; private set => SetProperty(ref _sourceHasAudio, value); }

    private string _statusMessage = "Open a source video to begin.";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private double _progressPercent;
    /// <summary>0-100 when a real fraction is known; meaningless while IsProgressIndeterminate.</summary>
    public double ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }

    private bool _isProgressIndeterminate = true;
    /// <summary>True while busy but no real completion fraction is available yet (e.g. probing).</summary>
    public bool IsProgressIndeterminate { get => _isProgressIndeterminate; set => SetProperty(ref _isProgressIndeterminate, value); }

    private ExportMode _exportMode = ExportMode.ReEncode;
    public ExportMode ExportMode { get => _exportMode; set => SetProperty(ref _exportMode, value); }

    private DetectionThresholds _thresholds = new();
    public DetectionThresholds Thresholds { get => _thresholds; private set => SetProperty(ref _thresholds, value); }

    private CutCandidate? _selectedCandidate;
    public CutCandidate? SelectedCandidate { get => _selectedCandidate; set => SetProperty(ref _selectedCandidate, value); }

    private double _currentTimeSeconds;
    /// <summary>Current playhead position, driven by the scrubber/player — not persisted.</summary>
    public double CurrentTimeSeconds { get => _currentTimeSeconds; set => SetProperty(ref _currentTimeSeconds, value); }

    private double _thumbnailWidth = 160;
    public double ThumbnailWidth { get => _thumbnailWidth; set => SetProperty(ref _thumbnailWidth, value); }

    private double _thumbnailHeight = 90;
    public double ThumbnailHeight { get => _thumbnailHeight; set => SetProperty(ref _thumbnailHeight, value); }

    public ObservableCollection<CutCandidate> Candidates { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ObservableCollection<ClipVerification> VerificationResults { get; } = new();

    public IAsyncRelayCommand<string> OpenFileCommand { get; }
    public IAsyncRelayCommand RedetectCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }

    public event Action? CandidatesReset;

    /// <summary>Fired once detection finishes, so the view can pop up a "you can interact now" notice.</summary>
    public event Action<string>? DetectionFinished;

    /// <summary>Fired once export+verification finishes, so the view can show the results popup.</summary>
    public event Action<string>? ExportFinished;

    /// <summary>
    /// Fired when an operation fails. The view shows this instead of the exception taking down
    /// the whole app — every risky method below catches internally and routes here.
    /// </summary>
    public event Action<string>? Faulted;

    private void ReportFault(string context, Exception ex)
    {
        Log.Error(context, ex);
        StatusMessage = $"Error: {context.ToLowerInvariant()}. See log for details.";
        Faulted?.Invoke($"{context}\n\n{ex.GetType().Name}: {ex.Message}\n\nFull details were written to:\n{Log.LogPath}");
    }

    public MainViewModel()
    {
        OpenFileCommand = new AsyncRelayCommand<string>(OpenFileAsync!);
        RedetectCommand = new AsyncRelayCommand(RedetectAsync, () => SourcePath != null);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => SourcePath != null && Candidates.Any(c => c.Approved));
    }

    /// <summary>
    /// Turns a service's (message, percent) progress report into StatusMessage + a real
    /// ProgressPercent, falling back to the indeterminate marquee only when percent is unknown
    /// (e.g. the initial probe, before any phase/clip count is known).
    /// </summary>
    private IProgress<(string Message, double? Percent)> MakeProgress() =>
        new Progress<(string Message, double? Percent)>(p =>
        {
            StatusMessage = p.Message;
            if (p.Percent.HasValue)
            {
                ProgressPercent = p.Percent.Value;
                IsProgressIndeterminate = false;
            }
            else
            {
                IsProgressIndeterminate = true;
            }
        });

    public async Task OpenFileAsync(string path)
    {
        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressPercent = 0;
        try
        {
            Log.Info($"OpenFileAsync: '{path}'");
            SourcePath = path;
            ThumbnailDir = Path.Combine(Path.GetTempPath(), "VideoCutter", Path.GetFileNameWithoutExtension(path));
            Warnings.Clear();
            VerificationResults.Clear();
            // Clear candidates from whatever file was previously open BEFORE anything else runs.
            // Without this, RunDetectionAsync's "preserve manually-added candidates from a prior
            // pass" logic (meant for the Re-run Detection button, same file) would instead carry
            // the PREVIOUS file's manual candidates into this unrelated new file.
            Candidates.Clear();
            CandidatesReset?.Invoke();

            StatusMessage = "Probing source...";
            var probe = await _probe.ProbeAsync(path);
            DurationSeconds = probe.DurationSeconds;
            SourceHasAudio = probe.HasAudio;

            if (probe.DurationSeconds <= 0)
            {
                StatusMessage = "Could not read a duration for this file — is it a valid video?";
                Log.Warn($"OpenFileAsync: zero/invalid duration probed for '{path}'");
                return;
            }

            var existing = await ProjectStore.LoadAsync(path);
            if (existing != null && Math.Abs(existing.DurationSeconds - probe.DurationSeconds) < 0.5)
            {
                Thresholds = existing.Thresholds;
                Candidates.Clear();
                foreach (var c in existing.Candidates.OrderBy(c => c.TimeSeconds))
                    Candidates.Add(c);
                StatusMessage = $"Resumed saved review ({Candidates.Count} candidates) from {ProjectStore.GetProjectPath(path)}.";
                CandidatesReset?.Invoke();
                await GenerateThumbnailsAsync();
                RedetectCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
                return;
            }

            await RunDetectionAsync();
        }
        catch (Exception ex)
        {
            ReportFault($"Opening '{Path.GetFileName(path)}' failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RedetectAsync() => await RunDetectionAsync();

    private async Task RunDetectionAsync()
    {
        if (SourcePath == null) return;
        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressPercent = 0;
        try
        {
            Warnings.Clear();
            VerificationResults.Clear(); // stale from a prior export of the old candidate set
            var outcome = await _detector.DetectAsync(SourcePath, DurationSeconds, SourceHasAudio, Thresholds, MakeProgress());

            foreach (var w in outcome.Warnings) Warnings.Add(w);

            // Best guess: a candidate confirmed by two or more independent detectors (e.g. a
            // black gap AND a scene cut) is pre-approved. Single-signal hits — especially scene
            // cuts, which also fire inside a single ad on quick edits — start unchecked for review.
            foreach (var c in outcome.Candidates)
                c.Approved = c.Sources.Distinct().Count() >= 2;

            // Preserve manually-added candidates from a prior pass.
            var manual = Candidates.Where(c => c.IsManual).ToList();

            Candidates.Clear();
            foreach (var c in outcome.Candidates.Concat(manual).OrderBy(c => c.TimeSeconds))
                Candidates.Add(c);

            CandidatesReset?.Invoke();
            var summary = $"Detection finished: {Candidates.Count} candidate cut points found, {Candidates.Count(c => c.Approved)} pre-approved.";
            StatusMessage = summary;

            await GenerateThumbnailsAsync();
            await AutosaveAsync();
            RedetectCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            DetectionFinished?.Invoke(summary);
        }
        catch (Exception ex)
        {
            ReportFault("Detection failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateThumbnailsAsync()
    {
        if (SourcePath == null || ThumbnailDir == null) return;
        int i = 0;
        foreach (var c in Candidates)
        {
            i++;
            if (c.ThumbnailPath != null && File.Exists(c.ThumbnailPath)) continue;
            try
            {
                c.ThumbnailPath = await Thumbnails.GenerateAsync(SourcePath, c.TimeSeconds, ThumbnailDir, $"cand_{i:D3}");
            }
            catch (Exception ex)
            {
                // Thumbnail failures (e.g. candidate too close to EOF) are non-fatal; the row just shows no image.
                Log.Warn($"Thumbnail generation failed for candidate at {c.TimeSeconds:F2}s: {ex.Message}");
            }
        }
    }

    public void AddCandidate(double timeSeconds)
    {
        var c = new CutCandidate
        {
            TimeSeconds = Math.Max(0, Math.Min(DurationSeconds, timeSeconds)),
            Sources = new List<CandidateSource> { CandidateSource.Manual },
            Approved = true,
        };
        Candidates.Add(c);
        SortCandidates();
        _ = GenerateThumbnailsAsync();
        ExportCommand.NotifyCanExecuteChanged();
    }

    public void RemoveCandidate(CutCandidate c)
    {
        Candidates.Remove(c);
        ExportCommand.NotifyCanExecuteChanged();
    }

    public void MoveCandidate(CutCandidate c, double newTimeSeconds)
    {
        c.TimeSeconds = Math.Max(0, Math.Min(DurationSeconds, newTimeSeconds));
        c.ThumbnailPath = null; // stale — regenerate for the new position
        SortCandidates();
        _ = GenerateThumbnailsAsync();
    }

    public void SortCandidates()
    {
        var sorted = Candidates.OrderBy(c => c.TimeSeconds).ToList();
        Candidates.Clear();
        foreach (var c in sorted) Candidates.Add(c);
        CandidatesReset?.Invoke();
    }

    public async Task AutosaveAsync()
    {
        if (SourcePath == null) return;
        var state = new ProjectState
        {
            SourcePath = SourcePath,
            DurationSeconds = DurationSeconds,
            SourceHasAudio = SourceHasAudio,
            Thresholds = Thresholds,
            Candidates = Candidates.ToList(),
        };
        await ProjectStore.SaveAsync(state);
    }

    public async Task ExportAsync()
    {
        if (SourcePath == null) return;
        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressPercent = 0;
        try
        {
            VerificationResults.Clear();
            var approvedTimes = Candidates.Where(c => c.Approved).Select(c => c.TimeSeconds).OrderBy(t => t).ToList();
            var plans = _exporter.PlanSegments(SourcePath, approvedTimes, DurationSeconds);

            await _exporter.ExportAsync(SourcePath, plans, ExportMode, MakeProgress());

            IsProgressIndeterminate = true;
            StatusMessage = "Verifying exported clips...";
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(SourcePath))!;
            var results = await _verifier.VerifyAsync(outputDir, plans, SourceHasAudio, DurationSeconds, MakeProgress());
            foreach (var r in results) VerificationResults.Add(r);

            var failCount = results.Count(r => !r.Passed);
            var summary = failCount == 0
                ? $"Exported and verified {plans.Count} clips — all passed."
                : $"Exported {plans.Count} clips — {failCount} verification issue(s), see below.";
            StatusMessage = summary;
            ExportFinished?.Invoke(summary);
        }
        catch (Exception ex)
        {
            ReportFault("Export failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string FormatTimecode(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.ToString(@"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
    }
}
