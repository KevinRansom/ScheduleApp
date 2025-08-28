using System;
using System.Windows.Input;

namespace ScheduleApp.Infrastructure
{
    // Parameterized relay command to receive SelectedItems from the view
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _can;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _can = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_can == null) return true;
            if (parameter == null && default(T) != null) return _can(default(T));
            return _can((T)parameter);
        }

        public void Execute(object parameter)
        {
            if (parameter == null && default(T) != null) { _execute(default(T)); return; }
            _execute((T)parameter);
        }

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}