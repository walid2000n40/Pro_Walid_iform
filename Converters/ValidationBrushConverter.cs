using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace ProWalid.Converters
{
    public class ValidationBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush InvalidBrush = new(ColorHelper.FromArgb(255, 211, 47, 47));
        private static readonly SolidColorBrush ValidBrush = new(ColorHelper.FromArgb(255, 93, 163, 114));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var parameterText = parameter?.ToString();

            if (parameterText == "numeric")
            {
                if (value is double doubleValue)
                {
                    return doubleValue > 0 ? ValidBrush : InvalidBrush;
                }

                if (value is int intValue)
                {
                    return intValue > 0 ? ValidBrush : InvalidBrush;
                }

                if (double.TryParse(value?.ToString(), out var parsed))
                {
                    return parsed > 0 ? ValidBrush : InvalidBrush;
                }

                return InvalidBrush;
            }

            var text = value?.ToString();
            return !string.IsNullOrWhiteSpace(text) ? ValidBrush : InvalidBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
