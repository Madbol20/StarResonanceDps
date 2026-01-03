using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// 将主题颜色字符串转换为渐变画刷
/// 输入: 颜色字符串 (如 "#6BA5FC")
/// 输出: LinearGradientBrush (浅色到主题颜色)
/// </summary>
public sealed class ThemeColorToGradientConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var colorString = value as string ?? "#BABABA";
        
        try
        {
            var themeColor = (Color)ColorConverter.ConvertFromString(colorString);
            
            // 创建浅色版本（增加亮度）
            var lightColor = Color.FromRgb(
                (byte)Math.Min(255, themeColor.R + (255 - themeColor.R) * 0.7),
                (byte)Math.Min(255, themeColor.G + (255 - themeColor.G) * 0.7),
                (byte)Math.Min(255, themeColor.B + (255 - themeColor.B) * 0.7)
            );
            
            // 创建渐变画刷
            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            
            gradientBrush.GradientStops.Add(new GradientStop(lightColor, 0));
            gradientBrush.GradientStops.Add(new GradientStop(themeColor, 1));
            
            return gradientBrush;
        }
        catch
        {
            // 默认灰色渐变
            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(228, 242, 253), 0));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(140, 177, 245), 1));
            return gradientBrush;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
