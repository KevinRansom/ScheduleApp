using System;
using System.Globalization;
using System.Windows.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Converters
{
    // Converter receives the entire CoverageTask (Binding=".") and returns the string to show
    // in the "Teacher" column: either the TeacherName or "Self" when appropriate.
    public class SupportTeacherDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is CoverageTask t)) return string.Empty;

            // If TeacherName is empty/null -> treat as Self
            if (string.IsNullOrWhiteSpace(t.TeacherName))
                return "Self";

            // If teacher equals support and it's a self-care kind, show Self
            if (string.Equals(t.TeacherName, t.SupportName, StringComparison.OrdinalIgnoreCase) &&
                (t.Kind == CoverageTaskKind.Break || t.Kind == CoverageTaskKind.Lunch || t.Kind == CoverageTaskKind.Idle))
            {
                return "Self";
            }

            return t.TeacherName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter for display only
            return Binding.DoNothing;
        }
    }
}