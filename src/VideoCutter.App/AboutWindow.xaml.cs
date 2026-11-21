using System.IO;
using System.Reflection;
using System.Windows;
using VideoCutter.App.Services;

namespace VideoCutter.App;

/// <summary>
/// Interaction logic for AboutWindow.xaml
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = info ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        VersionText.Text = $"Version {version}";

        SourceInitialized += (_, _) => ThemeManager.ApplyTitlebarToWindow(this);
    }

    private void ViewLicense_Click(object sender, RoutedEventArgs e) =>
        OpenRepoFileOrFallback("LICENSE",
            "This project is licensed under the MIT License. See the LICENSE file in the repository root.");

    private void ViewThirdPartyNotices_Click(object sender, RoutedEventArgs e) =>
        OpenRepoFileOrFallback("THIRD-PARTY-NOTICES.md",
            "VideoCutter bundles ffmpeg (GPLv3) and libvlc/LibVLCSharp (LGPL v2.1). See " +
            "THIRD-PARTY-NOTICES.md in the install/repository root for full details.");

    private void OpenRepoFileOrFallback(string fileName, string fallbackMessage)
    {
        try
        {
            // Ships copied right next to the exe (see VideoCutter.App.csproj Content items), so
            // this is normally found immediately; the directory walk-up is a backstop for
            // running straight out of bin/Debug in a dev checkout before a build has run.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            string? path = null;
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) { path = candidate; break; }
            }

            if (path != null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            else
                MessageBox.Show(fallbackMessage, "VideoCutter — License", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            MessageBox.Show(fallbackMessage, "VideoCutter — License", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
