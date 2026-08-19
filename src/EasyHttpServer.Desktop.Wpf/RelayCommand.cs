using System.Windows.Input;

namespace EasyHttpServer.Desktop.Wpf;

public sealed class RelayCommand(
    Action execute,
    Func<bool>? canExecute = null,
    Action<Exception>? errorHandler = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        try
        {
            execute();
        }
        catch (Exception exception) when (errorHandler is not null && CommandExceptionPolicy.IsRecoverable(exception))
        {
            errorHandler(exception);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
