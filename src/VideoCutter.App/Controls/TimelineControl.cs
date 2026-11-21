using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoCutter.Core.Models;

namespace VideoCutter.App.Controls;

/// <summary>
/// A duration-scaled strip of draggable markers. Click empty space to add a cut, drag a
/// marker to nudge it, click a marker to select it for preview. Approved markers are green,
/// unapproved candidates are gray, manually-added ones are blue.
/// </summary>
public class TimelineControl : Canvas
{
    public static readonly DependencyProperty DurationSecondsProperty =
        DependencyProperty.Register(nameof(DurationSeconds), typeof(double), typeof(TimelineControl),
            new FrameworkPropertyMetadata(0.0, (d, _) => ((TimelineControl)d).Rebuild()));

    public double DurationSeconds
    {
        get => (double)GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    public event Action<double>? CandidateAddRequested;
    public event Action<CutCandidate, double>? CandidateMoved;
    public event Action<CutCandidate>? CandidateSelected;

    private ObservableCollection<CutCandidate>? _candidates;
    public ObservableCollection<CutCandidate>? Candidates
    {
        get => _candidates;
        set
        {
            if (_candidates != null) _candidates.CollectionChanged -= OnCandidatesChanged;
            _candidates = value;
            if (_candidates != null) _candidates.CollectionChanged += OnCandidatesChanged;
            Rebuild();
        }
    }

    private readonly Rectangle _baseLine = new() { Height = 4, Fill = Brushes.Gray };

    public TimelineControl()
    {
        Height = 56;
        Background = Brushes.Transparent;
        Children.Add(_baseLine);
        SetTop(_baseLine, 26);
        MouseLeftButtonDown += OnCanvasClick;
        SizeChanged += (_, _) => Rebuild();
    }

    private void OnCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private double TimeToX(double t) => DurationSeconds <= 0 ? 0 : t / DurationSeconds * Math.Max(ActualWidth, 1);
    private double XToTime(double x) => DurationSeconds <= 0 ? 0 : x / Math.Max(ActualWidth, 1) * DurationSeconds;

    private void OnCanvasClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb) return; // the thumb's own handlers deal with that click
        var pos = e.GetPosition(this);
        var t = XToTime(pos.X);
        CandidateAddRequested?.Invoke(Math.Max(0, Math.Min(DurationSeconds, t)));
    }

    public void Rebuild()
    {
        foreach (var th in Children.OfType<Thumb>().ToList())
            Children.Remove(th);

        _baseLine.Width = Math.Max(ActualWidth, 1);
        if (_candidates == null) return;

        foreach (var c in _candidates)
        {
            var thumb = new Thumb
            {
                Width = 10,
                Height = 40,
                Background = c.IsManual ? Brushes.DodgerBlue : c.Approved ? Brushes.LimeGreen : Brushes.DarkGray,
                Tag = c,
                Cursor = Cursors.SizeWE,
                ToolTip = $"{c.TimeSeconds:F2}s — {c.SourceLabel}",
            };
            SetTop(thumb, 8);
            SetLeft(thumb, TimeToX(c.TimeSeconds) - thumb.Width / 2);

            thumb.DragDelta += (s, args) =>
            {
                var th = (Thumb)s!;
                var cand = (CutCandidate)th.Tag;
                var newX = Math.Max(0, Math.Min(Math.Max(ActualWidth, 1) - th.Width, GetLeft(th) + args.HorizontalChange));
                SetLeft(th, newX);
                CandidateMoved?.Invoke(cand, XToTime(newX + th.Width / 2));
            };
            thumb.PreviewMouseLeftButtonDown += (s, _) => CandidateSelected?.Invoke((CutCandidate)((Thumb)s!).Tag);

            Children.Add(thumb);
        }
    }
}
