using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using VideoCutter.App.Services;
using VideoCutter.Core.Models;
using VideoCutter.Core.Services;

namespace VideoCutter.App;

/// <summary>
/// Shows the outcome of one export pass (per-clip pass/fail + duration) with a button to save
/// the list to a file. Takes a snapshot of the results rather than binding to the live
/// collection, so this window's content stays frozen to the export it was opened for even if
/// the user starts another export in the main window while this is still open.
/// </summary>
public partial class ExportResultsWindow : Window
{
    private readonly List<ClipVerification> _results;
    private readonly string? _outputDir;

    public ExportResultsWindow(string summary, IEnumerable<ClipVerification> results, string? outputDir = null)
    {
        InitializeComponent();

        _results = results.ToList();
        _outputDir = outputDir;
        SummaryText.Text = summary;
        ResultsList.ItemsSource = _results;

        if (_outputDir != null)
        {
            FolderPathText.Text = _outputDir;
        }
        else
        {
            FolderPanel.Visibility = Visibility.Collapsed;
        }

        SourceInitialized += (_, _) => ThemeManager.ApplyTitlebarToWindow(this);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_outputDir == null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_outputDir) { UseShellExecute = true });
        }
        catch (System.Exception ex)
        {
            Log.Error($"Failed to open output folder '{_outputDir}'", ex);
            MessageBox.Show($"Could not open the folder: {ex.Message}\n\n{_outputDir}", "VideoCutter — Open Folder Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveList_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save export results",
            Filter = "Text file|*.txt|CSV file|*.csv|All files|*.*",
            FileName = "VideoCutter-export-results.txt",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = dlg.FileName.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)
                ? BuildCsv()
                : BuildPlainText();
            File.WriteAllLines(dlg.FileName, lines);
            Log.Info($"Export results list saved to '{dlg.FileName}'");
        }
        catch (System.Exception ex)
        {
            Log.Error($"Failed to save export results list to '{dlg.FileName}'", ex);
            MessageBox.Show($"Could not save the file: {ex.Message}", "VideoCutter — Save Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private IEnumerable<string> BuildPlainText()
    {
        yield return SummaryText.Text;
        yield return "";
        foreach (var r in _results)
            yield return $"{r.FileName}: {r.Message} ({r.DurationSeconds:F1}s)";
    }

    private IEnumerable<string> BuildCsv()
    {
        yield return "FileName,Passed,Message,DurationSeconds,HasAudio";
        foreach (var r in _results)
            yield return $"{Csv(r.FileName)},{r.Passed},{Csv(r.Message)},{r.DurationSeconds:F2},{r.HasAudio}";
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\""
        : s;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
