using System.Configuration;
using System.Data;
using System.Windows;
using LibVLCSharp.Shared;

namespace VideoCutter.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LibVLCSharp.Shared.Core.Initialize(); // fully qualified: "Core" would otherwise resolve to our own VideoCutter.Core namespace
    }
}

