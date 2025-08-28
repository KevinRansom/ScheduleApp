using System;
using System.Windows.Input;

namespace ScheduleApp.Infrastructure
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _can;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _can = canExecute;
        }

        public bool CanExecute(object parameter) { return _can == null || _can(); }
        public void Execute(object parameter) { _execute(); }

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    public static class BooleanConverter
    {
        public static readonly NullToBoolConverter ConvertNullToFalse = new NullToBoolConverter(false);
        public static readonly NullToBoolConverter ConvertNullToTrue  = new NullToBoolConverter(true);
    }

    public class NullToBoolConverter : System.Windows.Data.IValueConverter
    {
        private readonly bool _valueWhenNotNull;
        public NullToBoolConverter(bool valueWhenNotNull) { _valueWhenNotNull = valueWhenNotNull; }
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value != null ? _valueWhenNotNull : !_valueWhenNotNull;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) { return null; }
    }
}