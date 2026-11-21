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

    private void ViewLicense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // The LICENSE file ships at the repo root; find it relative to the build output
            // by walking up from the app's base directory (works for `dotnet run` and a built exe).
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            string? licensePath = null;
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "LICENSE");
                if (File.Exists(candidate)) { licensePath = candidate; break; }
            }

            if (licensePath != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(licensePath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(
                    "This project is licensed under the MIT License. See the LICENSE file in the repository root.",
                    "VideoCutter — License", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch
        {
            MessageBox.Show(
                "This project is licensed under the MIT License. See the LICENSE file in the repository root.",
                "VideoCutter — License", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
