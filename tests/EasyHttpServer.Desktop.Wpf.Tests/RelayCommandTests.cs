using EasyHttpServer.Desktop.Wpf;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class RelayCommandTests
{
    [Fact]
    public void ExecuteReportsRecoverableException()
    {
        Exception? reported = null;
        var command = new RelayCommand(
            () => throw new FormatException("test failure"),
            errorHandler: exception => reported = exception);

        command.Execute(null);

        Assert.IsType<FormatException>(reported);
    }

    [Fact]
    public void ExecuteWithoutHandlerPreservesException() =>
        Assert.Throws<FormatException>(() =>
            new RelayCommand(() => throw new FormatException("test failure")).Execute(null));
}
