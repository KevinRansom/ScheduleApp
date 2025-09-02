using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScheduleApp.Converters
{
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool v = value is bool b && b;
            return v ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is Visibility vis) ? (vis != Visibility.Visible) : (object)false;
    }
}