using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace ProWalid.Converters
{
    public class NumericTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue)
            {
                return Math.Abs(doubleValue) < 0.0001 ? string.Empty : doubleValue.ToString(CultureInfo.InvariantCulture);
            }

            if (value is int intValue)
            {
                return intValue == 0 ? string.Empty : intValue.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0d;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
            {
                return result;
            }

            return 0d;
        }
    }
}
