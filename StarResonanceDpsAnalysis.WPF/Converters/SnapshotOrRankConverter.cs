using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public sealed class SnapshotOrRankConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
return "[--]";
        }

        var isSnapshot = values[0] is bool b && b;
        
      if (isSnapshot)
{
      return "[Пьее]";
        }

        if (values[1] == null || values[1] == DependencyProperty.UnsetValue)
        {
            return "[--]";
        }

        if (values[1] is int index)
     {
    return $"[{index}]";
        }

        if (int.TryParse(values[1]?.ToString(), out var parsedIndex))
        {
  return $"[{parsedIndex}]";
        }

        return "[--]";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
