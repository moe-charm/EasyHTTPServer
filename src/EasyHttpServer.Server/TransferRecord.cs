namespace EasyHttpServer.Server;

public sealed record TransferRecord(
    DateTimeOffset Timestamp,
    string Method,
    string Path,
    int StatusCode,
    long? ContentLength,
    TimeSpan Duration);
