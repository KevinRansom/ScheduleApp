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
            // Defensive checks
            if (value == null || value == DependencyProperty.UnsetValue)
                return Brushes.Transparent;

            // If the binding yields a CoverageTask, prefer its properties (and detect "Unscheduled")
            if (value is CoverageTask task)
            {
                if (!string.IsNullOrWhiteSpace(task.SupportName) &&
                    string.Equals(task.SupportName, "Unscheduled", StringComparison.OrdinalIgnoreCase))
                {
                    return UnscheduledBrush ?? Brushes.Transparent;
                }

                return MapKindToBrush(task.Kind);
            }

            // If the binding yields a CoverageTaskKind directly
            if (value is CoverageTaskKind kind)
            {
                return MapKindToBrush(kind);
            }

            // Fallback: transparent brush
            return Brushes.Transparent;
        }

        private Brush MapKindToBrush(CoverageTaskKind kind)
        {
            switch (kind)
            {
                case CoverageTaskKind.Coverage: return CoverageBrush ?? Brushes.Transparent;
                case CoverageTaskKind.Break:    return BreakBrush ?? Brushes.Transparent;
                case CoverageTaskKind.Lunch:    return LunchBrush ?? Brushes.Transparent;
                case CoverageTaskKind.Idle:     return IdleBrush ?? Brushes.Transparent;
                default:                        return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}