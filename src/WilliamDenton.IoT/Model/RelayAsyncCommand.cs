using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WilliamDenton.IoT.Model
{
    public class RelayAsyncCommand : IAsyncCommand
    {

        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayAsyncCommand(Func<object, Task> execute)
            : this(execute, null)
        { }

        public RelayAsyncCommand(Func<object, Task> execute, Predicate<object> canExecute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }
            _canExecute = canExecute;
            _execute = execute;
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

        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        public Task ExecuteAsync(object parameter)
        {
            return _execute(parameter);
        }
    }
}
