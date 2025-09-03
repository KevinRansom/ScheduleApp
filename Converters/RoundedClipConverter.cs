using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ScheduleApp.Converters
{
    // MultiValue: [ActualWidth, ActualHeight, CornerRadius, optional stroke(Double)]
    // Inset by (stroke - 0.5) so inner fills touch the inner edge of a 1px stroke without overpainting it.
    public class RoundedClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
                return null;

            if (!(values[0] is double width) || !(values[1] is double height))
                return null;

            var cr = values[2] is CornerRadius c ? c : new CornerRadius(0);

            double stroke = 0;
            if (values.Length > 3 && values[3] is double s)
                stroke = Math.Max(0.0, s);

            if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
                return null;

            // Use a slight inset (stroke - 0.5) so inner content visually meets the inner edge of the stroke
            double inset = Math.Max(0.0, stroke - 0.5);

            double insetW = Math.Max(0.0, width - (inset * 2));
            double insetH = Math.Max(0.0, height - (inset * 2));
            if (insetW <= 0 || insetH <= 0) return null;

            // Use smallest corner and subtract inset so the clip aligns with the inner curve of the stroke
            double baseR = Math.Min(Math.Min(cr.TopLeft, cr.TopRight), Math.Min(cr.BottomLeft, cr.BottomRight));
            double radius = Math.Max(0.0, baseR - inset);

            var rect = new Rect(inset, inset, insetW, insetH);
            return new RectangleGeometry(rect, radius, radius);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}