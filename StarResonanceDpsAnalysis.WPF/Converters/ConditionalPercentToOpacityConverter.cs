using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts (opacityPercent, isMouseThroughEnabled) -> opacity double (0..1).
/// Applies opacity regardless of mouse-through state.
/// </summary>
public sealed class ConditionalPercentToOpacityConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 1)
            return 1d;

        var opacityPercent = values[0];
        // Mouse-through state is no longer used to override opacity

        return opacityPercent switch
        {
            // Scale percent to 0..1, support double/int/string like PercentToOpacityConverter
            double d => Math.Clamp(d / 100d, 0d, 1d),
            int i => Math.Clamp(i / 100d, 0d, 1d),
            string str when double.TryParse(str, NumberStyles.Any, culture, out var parsed) => Math.Clamp(parsed / 100d,
                0d, 1d),
            _ => 1d
        };
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}