using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoCutter.Core.Services;

namespace VideoCutter.App.Services;

public enum AppTheme { Light, Dark, Cyberpunk, Unix, Pink, Film, Win95 }

/// <summary>Everything one theme needs: app colors plus the real Windows 11 titlebar color.</summary>
public class ThemeDefinition
{
    public required string DisplayName { get; init; }
    public required Color WindowBackground { get; init; }
    public required Color PanelBackground { get; init; }
    public required Color Foreground { get; init; }
    public required Color Accent { get; init; }
    public required Color Border { get; init; }
    public required Color ControlBackground { get; init; }
    public string FontFamily { get; init; } = "Segoe UI";

    /// <summary>Null = leave the titlebar at the OS default.</summary>
    public Color? CaptionColor { get; init; }
    public Color? CaptionTextColor { get; init; }
    public bool ImmersiveDark { get; init; }

    /// <summary>Pack-URI relative path to a background image (Film theme only).</summary>
    public string? BackgroundImage { get; init; }
}

/// <summary>
/// Applies a theme's brushes to app-wide resources and, where supported (Windows 11 build
/// 22000+), the real OS window titlebar color via DWM — not just the client area.
/// </summary>
public static class ThemeManager
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static readonly Dictionary<AppTheme, ThemeDefinition> Themes = new()
    {
        [AppTheme.Light] = new ThemeDefinition
        {
            DisplayName = "Light",
            WindowBackground = Color.FromRgb(0xF5, 0xF5, 0xF5),
            PanelBackground = Colors.White,
            Foreground = Color.FromRgb(0x1A, 0x1A, 0x1A),
            Accent = Color.FromRgb(0x25, 0x63, 0xEB),
            Border = Color.FromRgb(0xCC, 0xCC, 0xCC),
            ControlBackground = Colors.White,
            CaptionColor = null,
            CaptionTextColor = null,
            ImmersiveDark = false,
        },
        [AppTheme.Dark] = new ThemeDefinition
        {
            DisplayName = "Dark",
            WindowBackground = Color.FromRgb(0x1E, 0x1E, 0x1E),
            PanelBackground = Color.FromRgb(0x25, 0x25, 0x26),
            Foreground = Color.FromRgb(0xE0, 0xE0, 0xE0),
            Accent = Color.FromRgb(0x3B, 0x82, 0xF6),
            Border = Color.FromRgb(0x3C, 0x3C, 0x3C),
            ControlBackground = Color.FromRgb(0x2D, 0x2D, 0x30),
            CaptionColor = Color.FromRgb(0x1E, 0x1E, 0x1E),
            CaptionTextColor = Colors.White,
            ImmersiveDark = true,
        },
        [AppTheme.Cyberpunk] = new ThemeDefinition
        {
            DisplayName = "Cyberpunk",
            WindowBackground = Color.FromRgb(0x0D, 0x02, 0x21),
            PanelBackground = Color.FromRgb(0x17, 0x0B, 0x33),
            Foreground = Color.FromRgb(0x00, 0xFF, 0xF7),
            Accent = Color.FromRgb(0xFF, 0x00, 0xC8),
            Border = Color.FromRgb(0xFF, 0x00, 0xC8),
            ControlBackground = Color.FromRgb(0x1B, 0x0E, 0x3D),
            CaptionColor = Color.FromRgb(0x0D, 0x02, 0x21),
            CaptionTextColor = Color.FromRgb(0x00, 0xFF, 0xF7),
            ImmersiveDark = true,
        },
        [AppTheme.Unix] = new ThemeDefinition
        {
            DisplayName = "Unix",
            WindowBackground = Colors.Black,
            PanelBackground = Color.FromRgb(0x0A, 0x0A, 0x0A),
            Foreground = Color.FromRgb(0x00, 0xFF, 0x41),
            Accent = Color.FromRgb(0x00, 0xFF, 0x41),
            Border = Color.FromRgb(0x00, 0x88, 0x22),
            ControlBackground = Colors.Black,
            FontFamily = "Consolas",
            CaptionColor = Colors.Black,
            CaptionTextColor = Color.FromRgb(0x00, 0xFF, 0x41),
            ImmersiveDark = true,
        },
        [AppTheme.Pink] = new ThemeDefinition
        {
            DisplayName = "Pink",
            WindowBackground = Color.FromRgb(0xFF, 0xE4, 0xF1),
            PanelBackground = Color.FromRgb(0xFF, 0xF0, 0xF7),
            Foreground = Color.FromRgb(0x8A, 0x15, 0x50),
            Accent = Color.FromRgb(0xFF, 0x69, 0xB4),
            Border = Color.FromRgb(0xFF, 0xB6, 0xD9),
            ControlBackground = Colors.White,
            CaptionColor = Color.FromRgb(0xFF, 0x69, 0xB4),
            CaptionTextColor = Colors.White,
            ImmersiveDark = false,
        },
        [AppTheme.Film] = new ThemeDefinition
        {
            DisplayName = "Film",
            WindowBackground = Color.FromRgb(0x1A, 0x14, 0x10),
            PanelBackground = Color.FromArgb(0xD0, 0x2A, 0x20, 0x18),
            Foreground = Color.FromRgb(0xF0, 0xE0, 0xC0),
            Accent = Color.FromRgb(0xC9, 0xA0, 0x63),
            Border = Color.FromRgb(0x6B, 0x4F, 0x2A),
            ControlBackground = Color.FromArgb(0xE0, 0x2A, 0x20, 0x18),
            BackgroundImage = "Resources/filmstrip.jpg",
            CaptionColor = Color.FromRgb(0x1A, 0x14, 0x10),
            CaptionTextColor = Color.FromRgb(0xF0, 0xE0, 0xC0),
            ImmersiveDark = true,
        },
        [AppTheme.Win95] = new ThemeDefinition
        {
            DisplayName = "Win95/98",
            WindowBackground = Color.FromRgb(0xC0, 0xC0, 0xC0),
            PanelBackground = Color.FromRgb(0xC0, 0xC0, 0xC0),
            Foreground = Colors.Black,
            Accent = Color.FromRgb(0x00, 0x00, 0x80),
            Border = Color.FromRgb(0x80, 0x80, 0x80),
            ControlBackground = Color.FromRgb(0xC0, 0xC0, 0xC0),
            FontFamily = "Tahoma",
            CaptionColor = Color.FromRgb(0x00, 0x00, 0x80),
            CaptionTextColor = Colors.White,
            ImmersiveDark = false,
        },
    };

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>Applies the theme's resources app-wide and, if the window is already shown, its titlebar.</summary>
    public static void Apply(AppTheme theme, Window? window)
    {
        Current = theme;
        var def = Themes[theme];
        var res = Application.Current.Resources;

        res["AppWindowBackgroundBrush"] = new SolidColorBrush(def.WindowBackground);
        res["AppPanelBackgroundBrush"] = new SolidColorBrush(def.PanelBackground);
        res["AppForegroundBrush"] = new SolidColorBrush(def.Foreground);
        res["AppAccentBrush"] = new SolidColorBrush(def.Accent);
        res["AppBorderBrush"] = new SolidColorBrush(def.Border);
        res["AppControlBackgroundBrush"] = new SolidColorBrush(def.ControlBackground);
        res["AppFontFamily"] = new FontFamily(def.FontFamily);

        if (def.BackgroundImage != null)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/{def.BackgroundImage}", UriKind.Absolute);
                res["AppBackgroundImageBrush"] = new ImageBrush(new BitmapImage(uri))
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0.55,
                };
                res["AppBackgroundImageVisibility"] = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not load theme background image '{def.BackgroundImage}': {ex.Message}");
                res["AppBackgroundImageVisibility"] = Visibility.Collapsed;
            }
        }
        else
        {
            res["AppBackgroundImageVisibility"] = Visibility.Collapsed;
        }

        if (window != null) ApplyTitlebar(window, def);
        SavePreference(theme);
    }

    /// <summary>Re-applies the current theme's titlebar colors to a specific window (e.g. a newly opened dialog).</summary>
    public static void ApplyTitlebarToWindow(Window window) => ApplyTitlebar(window, Themes[Current]);

    private static void ApplyTitlebar(Window window, ThemeDefinition def)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return; // not yet shown — caller should retry on SourceInitialized

            int dark = def.ImmersiveDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            int caption = def.CaptionColor is { } cc ? ToColorRef(cc) : unchecked((int)DWMWA_COLOR_DEFAULT);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));

            int text = def.CaptionTextColor is { } tc ? ToColorRef(tc) : unchecked((int)DWMWA_COLOR_DEFAULT);
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        catch (Exception ex)
        {
            // DWM titlebar theming needs Windows 11 22000+; older Windows just keeps the default
            // chrome. Not worth bothering the user about — the rest of the theme still applies.
            Log.Warn($"DWM titlebar theming unavailable ({ex.Message}); window chrome stays default.");
        }
    }

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoCutter", "settings.json");

    private static void SavePreference(AppTheme theme)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { Theme = theme.ToString() }));
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not save theme preference: {ex.Message}");
        }
    }

    public static AppTheme LoadPreference()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return AppTheme.Light;
            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Theme", out var el) &&
                Enum.TryParse<AppTheme>(el.GetString(), out var theme))
                return theme;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not load theme preference, defaulting to Light: {ex.Message}");
        }
        return AppTheme.Light;
    }
}
