namespace EasyHttpServer.Desktop.Wpf;

public sealed class SingleInstanceGuard : IDisposable
{
    public const string MutexName = @"Global\charmpic.EasyHTTPServer2.SingleInstance";

    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static SingleInstanceGuard? TryAcquire(string? mutexName = null)
    {
        var mutex = new Mutex(initiallyOwned: true, mutexName ?? MutexName, out var createdNew);
        if (createdNew)
        {
            return new SingleInstanceGuard(mutex, ownsMutex: true);
        }

        try
        {
            if (mutex.WaitOne(0))
            {
                return new SingleInstanceGuard(mutex, ownsMutex: true);
            }
        }
        catch (AbandonedMutexException)
        {
            return new SingleInstanceGuard(mutex, ownsMutex: true);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _ownsMutex = false;
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
