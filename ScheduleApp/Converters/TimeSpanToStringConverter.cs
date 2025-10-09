using System;
using System.Globalization;
using System.Windows.Data;

namespace ScheduleApp.Converters
{
    public sealed class TimeSpanToStringConverter : IValueConverter
    {
        private const string DefaultFormat = "hh\\:mm";

        private string _format = DefaultFormat;
        public string Format
        {
            get => _format;
            set => _format = string.IsNullOrWhiteSpace(value) ? DefaultFormat : value;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
            {
                try
                {
                    return ts.ToString(Format, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    // Fallback if an invalid format was supplied (e.g. "HH:mm" without escapes)
                    return ts.ToString(DefaultFormat, CultureInfo.InvariantCulture);
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts))
                return ts;

            return Binding.DoNothing;
        }
    }
}