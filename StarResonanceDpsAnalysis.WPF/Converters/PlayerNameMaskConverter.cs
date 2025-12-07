using System;
using System.Globalization;
using System.Windows.Data;
using System.Xml.Linq;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Masks player names based on provided flags.
/// </summary>
public class PlayerNameMaskConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4) return null;

        var rawName = values[0] as string ?? string.Empty;
        var uid = values[1]?.ToString() ?? string.Empty;
        var maskGlobal = values[2] is bool g && g;
        var maskTemp = values[3] is bool t && t;

        var useUid = string.IsNullOrEmpty(rawName);

        var showingName = useUid ? uid : rawName;
        if (maskGlobal || maskTemp)
        {
            showingName = MaskName(showingName);
        }
        
        return useUid ? $"UID: {showingName}" : showingName;
    }

    private static string MaskName(string name)
    {
        if (name.Length <= 1) return "*";
        if (name.Length == 2) return $"{name[0]}*";
        if (name.Length <= 5) return $"{name[0]}**{name[^1]}";
        return $"{name[..2]}**{name[^2..]}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
