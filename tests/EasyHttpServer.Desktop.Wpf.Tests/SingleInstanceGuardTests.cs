using EasyHttpServer.Desktop.Wpf;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuardIsRejectedAndNameCanBeReacquiredAfterDispose()
    {
        var name = $"Local\\EasyHttpServerSingleInstanceTests.{Guid.NewGuid():N}";
        using var first = Assert.IsType<SingleInstanceGuard>(SingleInstanceGuard.TryAcquire(name));

        SingleInstanceGuard? second = null;
        var contender = new Thread(() => second = SingleInstanceGuard.TryAcquire(name));
        contender.Start();
        contender.Join();
        Assert.Null(second);

        first.Dispose();
        using var reacquired = Assert.IsType<SingleInstanceGuard>(SingleInstanceGuard.TryAcquire(name));
    }
}
