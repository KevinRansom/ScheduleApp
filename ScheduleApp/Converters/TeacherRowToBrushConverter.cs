using System;
using System.Globalization;
using System.Windows;
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
            // Defensive: reject UnsetValue and always return a valid Brush
            if (value == DependencyProperty.UnsetValue)
                return FreeBrush ?? Brushes.Transparent;

            var row = value as TeacherScheduleRow;
            if (row == null) return FreeBrush ?? Brushes.Transparent;

            // Unscheduled takes precedence
            if (!string.IsNullOrWhiteSpace(row.SupportStaff) &&
                string.Equals(row.SupportStaff, "Unscheduled", StringComparison.OrdinalIgnoreCase))
            {
                return UnscheduledBrush ?? Brushes.White;
            }

            // Activity-based coloring
            if (string.Equals(row.Activity, "Break", StringComparison.OrdinalIgnoreCase))
                return BreakBrush ?? Brushes.Transparent;

            if (string.Equals(row.Activity, "Lunch", StringComparison.OrdinalIgnoreCase))
                return LunchBrush ?? Brushes.Transparent;

            // Start/End of Day -> Free color
            return FreeBrush ?? Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}