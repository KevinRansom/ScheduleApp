using System;
using System.Globalization;
using System.Windows.Data;

namespace ScheduleApp.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        // TimeSpan -> "HH:mm"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts) return ts.ToString(@"hh\:mm");
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && TimeSpan.TryParse(s, out var ts)) return ts;
            return Binding.DoNothing;
        }
    }
}