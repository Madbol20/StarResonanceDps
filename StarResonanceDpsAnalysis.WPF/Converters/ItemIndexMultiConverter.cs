using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converter that checks if an item's index in an ItemsControl is less than a parameter.
/// </summary>
public class ItemIndexMultiConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DependencyObject item && ItemsControl.ItemsControlFromItemContainer(item) is ItemsControl itemsControl)
        {
            int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
            if (int.TryParse(parameter?.ToString(), out int limit))
            {
                return index < limit;
            }
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}