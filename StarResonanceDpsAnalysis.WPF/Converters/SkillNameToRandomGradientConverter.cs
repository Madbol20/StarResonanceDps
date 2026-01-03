using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts skill name to a random (but consistent) gradient brush
/// Supports MultiBinding for more flexible usage
/// </summary>
public class SkillNameToRandomGradientConverter : IMultiValueConverter
{
    private static readonly List<(Color Start, Color End)> Gradients =
    [
        // Cyan/Blue
        (Color.FromRgb(0x22, 0xD3, 0xEE), Color.FromRgb(0x0E, 0xA5, 0xE9)),
        // Purple
        (Color.FromRgb(0xA7, 0x8B, 0xFA), Color.FromRgb(0x8B, 0x5C, 0xF6)),
        // Green/Teal
        (Color.FromRgb(0x34, 0xD3, 0x99), Color.FromRgb(0x10, 0xB9, 0x81)),
        // Darker Cyan
        (Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x08, 0x91, 0xB2)),
        // Light Blue
        (Color.FromRgb(0x60, 0xA5, 0xFA), Color.FromRgb(0x3B, 0x82, 0xF6)),
        // Magenta/Purple
        (Color.FromRgb(0xE8, 0x79, 0xF9), Color.FromRgb(0xD9, 0x46, 0xEF)),
        // Orange
        (Color.FromRgb(0xFB, 0xBF, 0x24), Color.FromRgb(0xF5, 0x9E, 0x0B)),
        // Red/Pink
        (Color.FromRgb(0xFB, 0x71, 0x85), Color.FromRgb(0xF4, 0x3F, 0x5E)),
        // Indigo
        (Color.FromRgb(0x81, 0x8C, 0xF8), Color.FromRgb(0x63, 0x66, 0xF1)),
        // Lime
        (Color.FromRgb(0xA3, 0xE6, 0x35), Color.FromRgb(0x84, 0xCC, 0x16)),
        // Pink/Rose
        (Color.FromRgb(0xF4, 0x72, 0xB6), Color.FromRgb(0xEC, 0x48, 0x99)),
        // Yellow
        (Color.FromRgb(0xFA, 0xCC, 0x15), Color.FromRgb(0xEA, 0xB3, 0x08)),
        // Violet
        (Color.FromRgb(0xC0, 0x84, 0xFC), Color.FromRgb(0xA8, 0x55, 0xF7)),
        // Emerald
        (Color.FromRgb(0x34, 0xD3, 0x99), Color.FromRgb(0x05, 0x9E, 0x69)),
        // Sky
        (Color.FromRgb(0x38, 0xBD, 0xF8), Color.FromRgb(0x0E, 0xA5, 0xE9))
    ];

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Try to get skill name from first value
        string? name = null;
        if (values != null && values.Length > 0)
        {
            name = values[0] as string;
        }

        if (string.IsNullOrEmpty(name))
        {
            // Default fallback
            return new LinearGradientBrush(
                Color.FromRgb(0x94, 0xA3, 0xB8), 
                Color.FromRgb(0x64, 0x74, 0x8B), 
                new System.Windows.Point(0, 0), 
                new System.Windows.Point(1, 1));
        }

        // Use a stable hash to ensure the same skill always gets the same color
        var index = Math.Abs(GetStableHashCode(name)) % Gradients.Count;
        var (start, end) = Gradients[index];

        return new LinearGradientBrush(start, end, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static int GetStableHashCode(string str)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in str)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}
