using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ScheduleApp.Models;

namespace ScheduleApp.Converters
{
    public class TeacherRowToBrushConverter : IValueConverter
    {
        public Brush BreakBrush { get; set; }
        public Brush LunchBrush { get; set; }
        public Brush FreeBrush { get; set; }
        public Brush UnscheduledBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var row = value as TeacherScheduleRow;
            if (row == null) return FreeBrush;

            // Unscheduled takes precedence
            if (!string.IsNullOrWhiteSpace(row.SupportStaff) &&
                string.Equals(row.SupportStaff, "Unscheduled", StringComparison.OrdinalIgnoreCase))
            {
                return UnscheduledBrush ?? Brushes.White;
            }

            // Activity-based coloring
            if (string.Equals(row.Activity, "Break", StringComparison.OrdinalIgnoreCase))
                return BreakBrush;

            if (string.Equals(row.Activity, "Lunch", StringComparison.OrdinalIgnoreCase))
                return LunchBrush;

            // Start/End of Day -> Free color
            return FreeBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}