using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ScheduleApp.Models;

namespace ScheduleApp.Converters
{
    public class TaskKindToBrushConverter : IValueConverter
    {
        public Brush CoverageBrush { get; set; }
        public Brush BreakBrush { get; set; }
        public Brush LunchBrush { get; set; }
        public Brush IdleBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Defensive: reject UnsetValue / null / wrong type and return a valid Brush
            if (value == null || value == DependencyProperty.UnsetValue)
                return Brushes.Transparent;

            if (!(value is CoverageTaskKind kind))
                return Brushes.Transparent;

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