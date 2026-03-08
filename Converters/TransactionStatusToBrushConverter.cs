using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace ProWalid.Converters
{
    public class TransactionStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var status = value as string;
            var mode = parameter as string;

            if (string.Equals(status, "تم التسليم", StringComparison.Ordinal))
            {
                return mode == "Foreground"
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 22, 101, 52))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 187, 247, 208));
            }

            return mode == "Foreground"
                ? new SolidColorBrush(ColorHelper.FromArgb(255, 124, 90, 0))
                : new SolidColorBrush(ColorHelper.FromArgb(255, 253, 230, 138));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
