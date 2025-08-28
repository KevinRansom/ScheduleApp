using System;
using System.Globalization;
using System.Windows.Data;

namespace ScheduleApp.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var ts = value as TimeSpan?;
            if (ts == null) return "";
            var t = ts.Value;
            return new DateTime(1, 1, 1, t.Hours, t.Minutes, 0).ToString("HH:mm");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TimeSpan ts;
            if (TimeSpan.TryParse(value as string, out ts))
                return ts;
            return TimeSpan.Zero;
        }
    }
}