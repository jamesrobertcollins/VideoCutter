using System.Configuration;
using System.Data;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VideoCutter.Core.Services;

namespace VideoCutter.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global safety nets: a crash with no log line is undiagnosable. These three cover the
        // three places an unhandled .NET exception can bring the process down — the UI thread,
        // any background thread, and a Task whose exception nobody awaited.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Log.Info($"=== VideoCutter starting === version={version} os={Environment.OSVersion} baseDir={AppContext.BaseDirectory}");
        Log.Info($"Log file for this and future sessions: {Log.LogPath}");

        try
        {
            LibVLCSharp.Shared.Core.Initialize(); // fully qualified: "Core" would otherwise resolve to our own VideoCutter.Core namespace
            Log.Info("LibVLC initialized OK.");
        }
        catch (Exception ex)
        {
            // Not fatal to the rest of the app (detection/export don't need LibVLC), so log and
            // continue rather than take the whole app down over a preview-only feature.
            Log.Error("LibVLC.Core.Initialize() failed — in-app video preview will not work", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("UNHANDLED (UI thread)", e.Exception);
        MessageBox.Show(
            $"VideoCutter hit an unexpected error and almost crashed:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n" +
            $"It's been logged to:\n{Log.LogPath}\n\nThe app will try to keep running.",
            "VideoCutter — Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // swallow it — a logged, visible error beats a silent crash
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Thrown from a non-UI thread; the process is going down regardless (can't set Handled
        // here), but this guarantees the reason is on disk before it does.
        Log.Error("UNHANDLED (background thread) — process is terminating", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "unknown"));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("Unobserved Task exception", e.Exception);
        e.SetObserved(); // prevent it from escalating to process termination on older runtimes
    }
}

