using System.Globalization;
using System.Windows.Data;

namespace BTWidget;

public sealed class RatioWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3
            || values[0] is not double value
            || values[1] is not double maximum
            || values[2] is not double width
            || maximum <= 0
            || width <= 0
            || !double.IsFinite(value)
            || !double.IsFinite(width))
        {
            return 0d;
        }

        return Math.Clamp(value / maximum, 0d, 1d) * width;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
