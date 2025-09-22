using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ScheduleApp.Models;

namespace ScheduleApp.Converters
{
    public class SupportTaskToBrushConverter : IValueConverter
    {
        public Brush CoverageBrush { get; set; }
        public Brush BreakBrush { get; set; }
        public Brush LunchBrush { get; set; }
        public Brush IdleBrush { get; set; }
        public Brush UnscheduledBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == DependencyProperty.UnsetValue) return IdleBrush ?? Brushes.Transparent;

            // Prefer full CoverageTask (Binding=".")
            if (value is CoverageTask task)
            {
                // detect unscheduled display keys like "Unscheduled" or "Unscheduled Breaks"
                if (!string.IsNullOrWhiteSpace(task.SupportName) &&
                    task.SupportName.IndexOf("unscheduled", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return UnscheduledBrush ?? Brushes.LightGray;
                }

                switch (task.Kind)
                {
                    case CoverageTaskKind.Coverage: return CoverageBrush ?? Brushes.LightGreen;
                    case CoverageTaskKind.Break:    return BreakBrush ?? Brushes.LightGoldenrodYellow;
                    case CoverageTaskKind.Lunch:    return LunchBrush ?? Brushes.LightSalmon;
                    case CoverageTaskKind.Idle:     return IdleBrush ?? Brushes.Transparent;
                    default:                        return Brushes.Transparent;
                }
            }

            // Back-compat: if enum was passed
            if (value is CoverageTaskKind kind)
            {
                switch (kind)
                {
                    case CoverageTaskKind.Coverage: return CoverageBrush ?? Brushes.LightGreen;
                    case CoverageTaskKind.Break:    return BreakBrush ?? Brushes.LightGoldenrodYellow;
                    case CoverageTaskKind.Lunch:    return LunchBrush ?? Brushes.LightSalmon;
                    case CoverageTaskKind.Idle:     return IdleBrush ?? Brushes.Transparent;
                    default:                        return Brushes.Transparent;
                }
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}