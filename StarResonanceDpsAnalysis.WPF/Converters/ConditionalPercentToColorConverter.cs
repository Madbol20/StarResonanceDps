using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts (opacityPercent, isMouseThroughEnabled) + base color (ConverterParameter) -> Color with alpha applied.
/// When isMouseThroughEnabled is true, returns base color with lower opacity to support click-through.
/// When isMouseThroughEnabled is false, applies the opacity percent normally.
/// Parameter can be a Color, SolidColorBrush, or string color (e.g., "#BABABA").
/// </summary>
public sealed class ConditionalPercentToColorConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        var baseColor = GetBaseColor(parameter);

        if (values is null || values.Length < 2)
            return baseColor;

        var factor = GetOpacityFactor(values[0], culture);
        var isMouseThroughEnabled = values[1] as bool? ?? (values[1] is string s && bool.TryParse(s, out var b) && b);
        if (!isMouseThroughEnabled)
        {
            return baseColor;
        }

        // Apply opacity normally regardless of mouse-through state
        var scaled = Math.Clamp(Math.Round(factor * 255d), 0d, 255d);
        baseColor.A = (byte)scaled;

        return baseColor;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        return [DependencyProperty.UnsetValue, DependencyProperty.UnsetValue];
    }

    private static Color GetBaseColor(object? parameter)
    {
        return parameter switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            string colorString when ColorConverter.ConvertFromString(colorString) is Color parsedColor => parsedColor,
            _ => Colors.Transparent
        };
    }

    private static double GetOpacityFactor(object? value, CultureInfo culture)
    {
        return value switch
        {
            double d when d <= 1d => Math.Clamp(d, 0d, 1d),
            double d => Math.Clamp(d / 100d, 0d, 1d),
            int i => Math.Clamp(i / 100d, 0d, 1d),
            string s when double.TryParse(s, NumberStyles.Any, culture, out var parsed) => Math.Clamp(parsed / 100d, 0d,
                1d),
            _ => 1d
        };
    }
}