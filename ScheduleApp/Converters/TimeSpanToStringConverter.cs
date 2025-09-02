using System;
using System.Globalization;
using System.Windows.Data;

namespace ScheduleApp.Converters
{
    public sealed class TimeSpanToStringConverter : IValueConverter
    {
        public string Format { get; set; } = "hh\\:mm";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts) return ts.ToString(Format, CultureInfo.InvariantCulture);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts)) return ts;
            return Binding.DoNothing;
        }
    }
}