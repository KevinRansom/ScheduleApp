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

        public bool CanExecute(object parameter) => _can == null || _can();
        public void Execute(object parameter) => _execute();

        // Hook into WPF's CommandManager so CanExecute refreshes automatically
        public event EventHandler CanExecuteChanged
        {
            add    { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    // Generic RelayCommand to accept CommandParameter (e.g., DataGrid.SelectedItems)
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _can;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _can = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_can == null) return true;
            return _can(AsT(parameter));
        }

        public void Execute(object parameter) => _execute(AsT(parameter));

        private static T AsT(object parameter) => parameter is T t ? t : default(T);

        // Same CommandManager wiring for auto refresh
        public event EventHandler CanExecuteChanged
        {
            add    { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
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
            => value != null ? _valueWhenNotNull : !_valueWhenNotNull;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => null;
    }
}