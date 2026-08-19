namespace EasyHttpServer.Server;

public interface IServerController : IAsyncDisposable
{
    bool IsRunning { get; }

    IReadOnlyList<Uri> BaseAddresses { get; }

    event EventHandler? StateChanged;

    event EventHandler<TransferRecord>? TransferCompleted;

    Task StartAsync(ServerOptions options, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
