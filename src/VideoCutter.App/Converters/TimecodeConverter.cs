using System.Globalization;
using System.Windows.Data;

namespace VideoCutter.App.Converters;

/// <summary>Formats a double seconds value as hh:mm:ss.ff for display, and parses it back on edit.</summary>
public class TimecodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var seconds = value is double d ? d : 0.0;
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.ToString(@"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "0";
        if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out var ts))
            return ts.TotalSeconds;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts))
            return ts.TotalSeconds;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return seconds;
        return Binding.DoNothing;
    }
}
