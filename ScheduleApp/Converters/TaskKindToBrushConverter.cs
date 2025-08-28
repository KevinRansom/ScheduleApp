using System;
using System.Globalization;
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
            var kind = (CoverageTaskKind)value;
            switch (kind)
            {
                case CoverageTaskKind.Coverage: return CoverageBrush;
                case CoverageTaskKind.Break: return BreakBrush;
                case CoverageTaskKind.Lunch: return LunchBrush;
                case CoverageTaskKind.Idle: return IdleBrush;
                default: return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { return null; }
    }
}