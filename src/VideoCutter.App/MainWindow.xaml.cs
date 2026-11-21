using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Microsoft.Win32;
using VideoCutter.App.Services;
using VideoCutter.App.ViewModels;
using VideoCutter.Core.Models;
using VideoCutter.Core.Services;

namespace VideoCutter.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _suppressScrubberSeek;
    private bool _uiReady;
    private readonly List<MenuItem> _themeMenuItems = new();
    private Dictionary<ThumbnailSize, MenuItem> _thumbnailSizeMenuItems = new();

    private static readonly double[] SpeedOptions = { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 4.0 };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        try
        {
            _libVlc = new LibVLC();
            _player = new MediaPlayer(_libVlc);
            VideoViewer.MediaPlayer = _player;
            _player.TimeChanged += Player_TimeChanged;
        }
        catch (Exception ex)
        {
            // Preview is a convenience, not core functionality — detect/export/verify all work
            // without it. Log and carry on with _player null rather than take the app down.
            Log.Error("Failed to initialize LibVLC — preview/scrubber will be unavailable", ex);
            MessageBox.Show(
                $"Video preview could not start ({ex.Message}). Detection and export will still work.\n\nSee log: {Log.LogPath}",
                "VideoCutter — Preview Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Timeline.Candidates = _vm.Candidates;
        Timeline.CandidateAddRequested += t => _vm.AddCandidate(t);
        Timeline.CandidateMoved += (c, t) => _vm.MoveCandidate(c, t);
        Timeline.CandidateSelected += c => { _vm.SelectedCandidate = c; PlayAround(c); };
        Timeline.CandidateRemoveRequested += c => _vm.RemoveCandidate(c);

        _vm.CandidatesReset += () => Dispatcher.Invoke(() => Timeline.Rebuild());
        _vm.DetectionFinished += summary => Dispatcher.Invoke(() =>
            MessageBox.Show(summary, "VideoCutter — Detection Finished", MessageBoxButton.OK, MessageBoxImage.Information));
        _vm.Faulted += message => Dispatcher.Invoke(() =>
            MessageBox.Show(message, "VideoCutter — Error", MessageBoxButton.OK, MessageBoxImage.Error));

        _autosaveTimer.Tick += async (_, _) =>
        {
            if (_vm.SourcePath != null) await _vm.AutosaveAsync();
        };
        _autosaveTimer.Start();

        SourceInitialized += (_, _) => ThemeManager.ApplyTitlebarToWindow(this);
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        Closing += (_, _) =>
        {
            Log.Info("=== VideoCutter closing ===");
            try
            {
                _player?.Stop();
                _player?.Dispose();
                _libVlc?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Error during shutdown cleanup (non-fatal, app is closing anyway)", ex);
            }
        };

        // Initial setup, done directly (not through the event handlers, which are guarded by
        // _uiReady and would otherwise no-op or run against not-yet-valid named elements).
        var savedTheme = ThemeManager.LoadPreference();
        foreach (var kv in ThemeManager.Themes)
        {
            var mi = new MenuItem { Header = kv.Value.DisplayName, IsCheckable = true, Tag = kv.Key, IsChecked = kv.Key == savedTheme };
            mi.Click += ThemeMenuItem_Click;
            _themeMenuItems.Add(mi);
            ThemeMenu.Items.Add(mi);
        }
        ThemeManager.Apply(savedTheme, null); // resources now; titlebar once SourceInitialized fires

        var savedThumbSize = ThemeManager.LoadThumbnailSizePreference();
        _thumbnailSizeMenuItems = new Dictionary<ThumbnailSize, MenuItem>
        {
            [ThumbnailSize.XS] = ThumbXSMenuItem,
            [ThumbnailSize.S] = ThumbSMenuItem,
            [ThumbnailSize.M] = ThumbMMenuItem,
            [ThumbnailSize.L] = ThumbLMenuItem,
            [ThumbnailSize.XL] = ThumbXLMenuItem,
        };
        ApplyThumbnailSize(savedThumbSize);

        foreach (var s in SpeedOptions)
            SpeedCombo.Items.Add(new ComboBoxItem { Content = $"{s}x", Tag = s });
        SpeedCombo.SelectedIndex = Array.IndexOf(SpeedOptions, 1.0);

        ApplyReviewLayout(videoOnTop: false);

        _uiReady = true;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return; // don't steal arrow keys from normal text editing
        if (_player == null) return;

        // Alt held down routes arrow keys through the "system key" path (menu-access convention).
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != Key.Left && key != Key.Right) return;

        double deltaSeconds = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 5.0
            : Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? 10.0
            : 1.0;
        if (key == Key.Left) deltaSeconds = -deltaSeconds;

        var newSeconds = Math.Max(0, Math.Min(_vm.DurationSeconds, _vm.CurrentTimeSeconds + deltaSeconds));
        _player.Time = (long)(newSeconds * 1000);
        _vm.CurrentTimeSeconds = newSeconds;
        e.Handled = true;
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        if (sender is not MenuItem clicked || clicked.Tag is not AppTheme theme) return;

        // Manual radio behavior: checking one unchecks all the others.
        foreach (var mi in _themeMenuItems) mi.IsChecked = ReferenceEquals(mi, clicked);
        ThemeManager.Apply(theme, this);
    }

    private void ThumbnailSizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        if (sender is not MenuItem clicked || clicked.Tag is not string tagStr) return;
        if (!Enum.TryParse<ThumbnailSize>(tagStr, out var size)) return;

        ApplyThumbnailSize(size);
        ThemeManager.SaveThumbnailSizePreference(size);
    }

    private void ApplyThumbnailSize(ThumbnailSize size)
    {
        foreach (var kv in _thumbnailSizeMenuItems) kv.Value.IsChecked = kv.Key == size;
        var (width, height, _) = ThumbnailSizes.Sizes[size];
        _vm.ThumbnailWidth = width;
        _vm.ThumbnailHeight = height;
    }

    private void SpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _player == null) return;
        if (SpeedCombo.SelectedItem is not ComboBoxItem item || item.Tag is not double speed) return;
        try { _player.SetRate((float)speed); }
        catch (Exception ex) { Log.Warn($"SetRate({speed}) failed: {ex.Message}"); }
    }

    private void LayoutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        if (sender is not MenuItem clicked) return;

        bool videoOnTop = ReferenceEquals(clicked, LayoutTopMenuItem);
        LayoutRightMenuItem.IsChecked = !videoOnTop;
        LayoutTopMenuItem.IsChecked = videoOnTop;
        ApplyReviewLayout(videoOnTop);
    }

    /// <summary>
    /// Rearranges the single ReviewGrid between "video on the right" (list 2*, video 3* — video
    /// gets the bigger share) and "video on top, list below" without duplicating any XAML: the
    /// same ListBox/GridSplitter/DockPanel instances just move to different cells.
    /// </summary>
    private void ApplyReviewLayout(bool videoOnTop)
    {
        if (videoOnTop)
        {
            ReviewCol0.Width = new GridLength(1, GridUnitType.Star);
            ReviewCol1.Width = new GridLength(0);
            ReviewCol2.Width = new GridLength(0);
            ReviewRow0.Height = new GridLength(2, GridUnitType.Star);
            ReviewRow1.Height = new GridLength(8);
            ReviewRow2.Height = new GridLength(3, GridUnitType.Star);

            Grid.SetRow(PreviewPanel, 0); Grid.SetColumn(PreviewPanel, 0);
            Grid.SetRow(ReviewSplitter, 1); Grid.SetColumn(ReviewSplitter, 0);
            Grid.SetRow(CandidateList, 2); Grid.SetColumn(CandidateList, 0);

            ReviewSplitter.Width = double.NaN;
            ReviewSplitter.Height = 8;
            ReviewSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ReviewSplitter.VerticalAlignment = VerticalAlignment.Center;
            ReviewSplitter.ResizeDirection = GridResizeDirection.Rows;
        }
        else
        {
            // List gets 2 shares, video gets 3 — the video window is the bigger of the two.
            ReviewCol0.Width = new GridLength(2, GridUnitType.Star);
            ReviewCol1.Width = new GridLength(8);
            ReviewCol2.Width = new GridLength(3, GridUnitType.Star);
            ReviewRow0.Height = new GridLength(1, GridUnitType.Star);
            ReviewRow1.Height = new GridLength(0);
            ReviewRow2.Height = new GridLength(0);

            Grid.SetRow(CandidateList, 0); Grid.SetColumn(CandidateList, 0);
            Grid.SetRow(ReviewSplitter, 0); Grid.SetColumn(ReviewSplitter, 1);
            Grid.SetRow(PreviewPanel, 0); Grid.SetColumn(PreviewPanel, 2);

            ReviewSplitter.Width = 8;
            ReviewSplitter.Height = double.NaN;
            ReviewSplitter.HorizontalAlignment = HorizontalAlignment.Center;
            ReviewSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            ReviewSplitter.ResizeDirection = GridResizeDirection.Columns;
        }
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private void Player_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var seconds = e.Time / 1000.0;
            _suppressScrubberSeek = true;
            try
            {
                _vm.CurrentTimeSeconds = seconds;
                if (seconds >= Scrubber.Minimum && seconds <= Scrubber.Maximum)
                    Scrubber.Value = seconds;
            }
            finally
            {
                _suppressScrubberSeek = false;
            }
        });
    }

    private void Scrubber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressScrubberSeek || _player == null) return;
        _player.Time = (long)(e.NewValue * 1000);
        _vm.CurrentTimeSeconds = e.NewValue;
    }

    private void AddCutHere_Click(object sender, RoutedEventArgs e) => _vm.AddCandidate(_vm.CurrentTimeSeconds);

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open source video",
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.ts;*.m2ts;*.wmv|All files|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        Log.Info($"OpenButton_Click: user selected '{dlg.FileName}'");

        try
        {
            await _vm.OpenFileCommand.ExecuteAsync(dlg.FileName);
        }
        catch (Exception ex)
        {
            // MainViewModel.OpenFileAsync catches internally and reports via Faulted, so this is
            // a backstop for anything outside that (e.g. a bug in the command plumbing itself).
            Log.Error("OpenFileCommand.ExecuteAsync threw past the ViewModel's own handling", ex);
            MessageBox.Show($"Failed to open file: {ex.Message}\n\nSee log: {Log.LogPath}",
                "VideoCutter — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_vm.SourcePath != null && _libVlc != null)
        {
            try
            {
                using var media = new Media(_libVlc, _vm.SourcePath, FromType.FromPath);
                _player?.Play(media);
                _player?.SetPause(true); // load paused; the user plays on demand
            }
            catch (Exception ex)
            {
                // Preview failing to load (unsupported codec for libvlc's bundled plugins, odd
                // path, etc.) must not take down detection/export, which don't depend on this.
                Log.Error($"Failed to load '{_vm.SourcePath}' into the LibVLC preview player", ex);
                MessageBox.Show(
                    $"Preview could not load this file ({ex.Message}). Detection/export are unaffected.\n\nSee log: {Log.LogPath}",
                    "VideoCutter — Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void RedetectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.RedetectCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error("RedetectCommand.ExecuteAsync threw past the ViewModel's own handling", ex);
            MessageBox.Show($"Re-detection failed: {ex.Message}\n\nSee log: {Log.LogPath}",
                "VideoCutter — Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.ExportCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error("ExportCommand.ExecuteAsync threw past the ViewModel's own handling", ex);
            MessageBox.Show($"Export failed: {ex.Message}\n\nSee log: {Log.LogPath}",
                "VideoCutter — Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{Log.LogPath}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Log file is at:\n{Log.LogPath}\n\n(Could not open Explorer: {ex.Message})",
                "VideoCutter — Log Location", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ApprovedCheck_Changed(object sender, RoutedEventArgs e) => Timeline.Rebuild();

    private void TimecodeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not CutCandidate candidate) return;
        var binding = tb.GetBindingExpression(TextBox.TextProperty);
        binding?.UpdateSource();
        _vm.MoveCandidate(candidate, candidate.TimeSeconds);
    }

    private void PlayCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CutCandidate candidate })
        {
            _vm.SelectedCandidate = candidate;
            PlayAround(candidate);
        }
    }

    private void RemoveCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CutCandidate candidate })
            _vm.RemoveCandidate(candidate);
    }

    private void PlayAround(CutCandidate? candidate)
    {
        if (candidate == null || _player == null) return;
        try
        {
            _player.Time = (long)(candidate.TimeSeconds * 1000);
            _player.SetPause(false);
        }
        catch (Exception ex)
        {
            Log.Error($"PlayAround failed at {candidate.TimeSeconds:F2}s", ex);
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_player == null) return;
        if (_player.IsPlaying) _player.SetPause(true);
        else _player.SetPause(false);
    }

    private void StopPreview_Click(object sender, RoutedEventArgs e) => _player?.SetPause(true);

    private void ReEncodeRadio_Checked(object sender, RoutedEventArgs e) => _vm.ExportMode = ExportMode.ReEncode;

    private void StreamCopyRadio_Checked(object sender, RoutedEventArgs e) => _vm.ExportMode = ExportMode.StreamCopy;
}