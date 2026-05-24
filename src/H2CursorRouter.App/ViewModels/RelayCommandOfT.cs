using System.Windows.Input;

namespace H2CursorRouter.App.ViewModels;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Predicate<T>? _canExecute;

    public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        parameter is T typedParameter && (_canExecute?.Invoke(typedParameter) ?? true);

    public void Execute(object? parameter)
    {
        if (parameter is T typedParameter && CanExecute(parameter))
        {
            _execute(typedParameter);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
