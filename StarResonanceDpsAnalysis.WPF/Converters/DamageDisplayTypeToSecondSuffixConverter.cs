using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts DamageDisplayType to appropriate "per second" suffix.
/// DamageDisplayType: 0 = English (K/M/B) ¡ú "/s"
/// DamageDisplayType: 1 = Chinese (Íò) ¡ú "/Ãë"
/// </summary>
public class DamageDisplayTypeToSecondSuffixConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int displayType)
        {
            return displayType == 0 ? "/s" : "/Ãë";
        }

        // Fallback to Chinese
        return "/Ãë";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
