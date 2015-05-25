using System;
using System.Diagnostics;
using System.Windows.Input;

namespace WilliamDenton.IoT.Model
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;


        public RelayCommand(Action<object> execute)
        : this(execute, null)
        { }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            _execute = execute;
            _canExecute = canExecute;
        }

        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return true;
            // return _canExecute == null ? true : _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged;
        //{
        //    add { System.Windows.Input.CommandManager.RequerySuggested += value; }
        //    remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        //}

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

    }

}
