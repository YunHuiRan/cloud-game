using System;
using System.Windows.Input;

namespace Ludens.Infrastructure
{
    /// <summary>Lightweight ICommand implementation without an external MVVM library.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action execute)
            : this(new Action<object>(delegate { execute(); }), null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
            : this(new Action<object>(delegate { execute(); }),
                canExecute == null ? null : new Func<object, bool>(delegate { return canExecute(); }))
        {
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            EventHandler handler = CanExecuteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}