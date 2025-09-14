using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScheduleApp.Converters
{
    // Returns CornerRadius: first header => (R,0,0,0), last => (0,R,0,0), others => 0
    public sealed class HeaderCornerRadiusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int displayIndex = TryInt(values, 0, -1);
            int count = TryInt(values, 1, 0);

            double r = 0;
            if (parameter is CornerRadius cr) r = cr.TopLeft;
            else if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) r = d;

            if (displayIndex == 0)
                return new CornerRadius(r, 0, 0, 0);
            if (count > 0 && displayIndex == count - 1)
                return new CornerRadius(0, r, 0, 0);

            return new CornerRadius(0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static int TryInt(object[] values, int index, int fallback)
            => values != null && values.Length > index && values[index] is int i ? i : fallback;
    }

    // Draws right and bottom gridlines; removes right line for the last header so the outer Border draws it
    public sealed class HeaderBorderThicknessConverter : IMultiValueConverter
    {
        private static readonly Thickness Mid = new Thickness(0, 0, 1, 1);
        private static readonly Thickness Last = new Thickness(0, 0, 0, 1);

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int displayIndex = values != null && values.Length > 0 && values[0] is int di ? di : -1;
            int count = values != null && values.Length > 1 && values[1] is int c ? c : 0;

            return (count > 0 && displayIndex == count - 1) ? Last : Mid;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}