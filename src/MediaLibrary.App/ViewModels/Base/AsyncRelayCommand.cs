using System.Windows.Input;

namespace MediaLibrary.App.ViewModels.Base;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private readonly bool _disableWhileExecuting;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, bool disableWhileExecuting = true)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute(), disableWhileExecuting)
    {
    }

    public AsyncRelayCommand(
        Func<object?, Task> execute,
        Predicate<object?>? canExecute = null,
        bool disableWhileExecuting = true)
    {
        _execute = execute;
        _canExecute = canExecute;
        _disableWhileExecuting = disableWhileExecuting;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return (!_disableWhileExecuting || !_isExecuting)
               && (_canExecute?.Invoke(parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            if (_disableWhileExecuting)
            {
                RaiseCanExecuteChanged();
            }

            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            if (_disableWhileExecuting)
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
