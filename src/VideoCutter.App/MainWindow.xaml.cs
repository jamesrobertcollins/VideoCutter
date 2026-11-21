using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Microsoft.Win32;
using VideoCutter.App.ViewModels;
using VideoCutter.Core.Models;

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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        _libVlc = new LibVLC();
        _player = new MediaPlayer(_libVlc);
        VideoViewer.MediaPlayer = _player;

        Timeline.Candidates = _vm.Candidates;
        Timeline.CandidateAddRequested += t => _vm.AddCandidate(t);
        Timeline.CandidateMoved += (c, t) => _vm.MoveCandidate(c, t);
        Timeline.CandidateSelected += c => { _vm.SelectedCandidate = c; PlayAround(c); };

        _vm.CandidatesReset += () => Dispatcher.Invoke(() => Timeline.Rebuild());

        _autosaveTimer.Tick += async (_, _) =>
        {
            if (_vm.SourcePath != null) await _vm.AutosaveAsync();
        };
        _autosaveTimer.Start();

        Closing += (_, _) =>
        {
            _player?.Stop();
            _player?.Dispose();
            _libVlc?.Dispose();
        };
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open source video",
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.ts;*.m2ts;*.wmv|All files|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        await _vm.OpenFileCommand.ExecuteAsync(dlg.FileName);

        if (_vm.SourcePath != null && _libVlc != null)
        {
            using var media = new Media(_libVlc, _vm.SourcePath, FromType.FromPath);
            _player?.Play(media);
            _player?.SetPause(true); // load paused; the user plays on demand
        }
    }

    private async void RedetectButton_Click(object sender, RoutedEventArgs e)
        => await _vm.RedetectCommand.ExecuteAsync(null);

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
        => await _vm.ExportCommand.ExecuteAsync(null);

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
        _player.Time = (long)(candidate.TimeSeconds * 1000);
        _player.SetPause(false);
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