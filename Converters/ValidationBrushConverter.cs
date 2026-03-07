using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace ProWalid.Converters
{
    public class ValidationBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var parameterText = parameter?.ToString();

            if (parameterText == "numeric")
            {
                if (value is double doubleValue)
                {
                    return new SolidColorBrush(doubleValue > 0 ? Colors.ForestGreen : Colors.IndianRed);
                }

                if (value is int intValue)
                {
                    return new SolidColorBrush(intValue > 0 ? Colors.ForestGreen : Colors.IndianRed);
                }

                if (double.TryParse(value?.ToString(), out var parsed))
                {
                    return new SolidColorBrush(parsed > 0 ? Colors.ForestGreen : Colors.IndianRed);
                }

                return new SolidColorBrush(Colors.IndianRed);
            }

            var text = value?.ToString();
            return new SolidColorBrush(!string.IsNullOrWhiteSpace(text) ? Colors.ForestGreen : Colors.IndianRed);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
