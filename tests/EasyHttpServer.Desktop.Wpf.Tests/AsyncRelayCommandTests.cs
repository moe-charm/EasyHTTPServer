using EasyHttpServer.Desktop.Wpf;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsyncReportsRecoverableExceptionAndResetsState()
    {
        Exception? reported = null;
        var command = new AsyncRelayCommand(
            () => throw new FormatException("test failure"),
            errorHandler: exception => reported = exception);

        await command.ExecuteAsync();

        Assert.IsType<FormatException>(reported);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ExecuteAsyncWithoutHandlerPreservesException()
    {
        var command = new AsyncRelayCommand(() => throw new FormatException("test failure"));

        await Assert.ThrowsAsync<FormatException>(() => command.ExecuteAsync());

        Assert.True(command.CanExecute(null));
    }
}
