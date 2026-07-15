using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsSipPhone.UI.Converters
{
    /// <summary>
    /// Converts a string to Visibility. Returns Visible if string is not null/empty, Collapsed otherwise.
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return string.IsNullOrEmpty(stringValue) ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This conversion is inherently lossy (Visibility -> string has no
            // meaningful inverse), so we decline the back-conversion instead of
            // throwing, which would crash any two-way binding using this converter.
            return Binding.DoNothing;
        }
    }
}
