using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// 将RGB值转换为Color
/// 输入: [0] Red (0-255), [1] Green (0-255), [2] Blue (0-255)
/// 输出: Color
/// </summary>
public sealed class RgbToColorConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3) 
            return Colors.Gray;

        try
        {
            var r = System.Convert.ToByte(values[0]);
            var g = System.Convert.ToByte(values[1]);
            var b = System.Convert.ToByte(values[2]);

            return Color.FromRgb(r, g, b);
        }
        catch
        {
            return Colors.Gray;
        }
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return new object[] { (double)color.R, (double)color.G, (double)color.B };
        }
        return new object[] { 186.0, 186.0, 186.0 };
    }
}
