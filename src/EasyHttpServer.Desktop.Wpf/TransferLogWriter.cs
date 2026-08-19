using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf;

public interface ITransferLogWriter : IAsyncDisposable
{
    Exception? LastError { get; }

    bool TryWrite(TransferRecord record);
}

public sealed class JsonLinesTransferLogWriter : ITransferLogWriter
{
    public const long DefaultMaximumFileBytes = 5 * 1024 * 1024;
    public const int DefaultRetainedFileCount = 5;
    private const int MaximumLoggedPathLength = 2048;
    private readonly string _filePath;
    private readonly long _maximumFileBytes;
    private readonly int _retainedFileCount;
    private readonly Channel<TransferRecord> _channel;
    private readonly Task _pump;
    private int _disposed;

    public JsonLinesTransferLogWriter(
        string filePath,
        long maximumFileBytes = DefaultMaximumFileBytes,
        int retainedFileCount = DefaultRetainedFileCount,
        int queueCapacity = 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(retainedFileCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(queueCapacity, 1);

        _filePath = Path.GetFullPath(filePath);
        _maximumFileBytes = maximumFileBytes;
        _retainedFileCount = retainedFileCount;
        _channel = Channel.CreateBounded<TransferRecord>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _pump = PumpAsync();
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EasyHTTPServer",
        "logs",
        "transfers.jsonl");

    public Exception? LastError { get; private set; }

    public bool TryWrite(TransferRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Volatile.Read(ref _disposed) == 0 && _channel.Writer.TryWrite(record);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();
        await _pump.ConfigureAwait(false);
    }

    private async Task PumpAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("Log path has no parent directory.");
            Directory.CreateDirectory(directory);

            await foreach (var record in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                var entry = LogEntry.From(record);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(entry, LogJsonContext.Default.LogEntry);
                if (GetCurrentLength() > 0 && GetCurrentLength() + bytes.Length + 1 > _maximumFileBytes)
                {
                    Rotate();
                }

                await using var stream = new FileStream(
                    _filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                await stream.WriteAsync("\n"u8.ToArray()).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = exception;
        }
    }

    private long GetCurrentLength() => File.Exists(_filePath) ? new FileInfo(_filePath).Length : 0;

    private void Rotate()
    {
        if (_retainedFileCount == 1)
        {
            File.Delete(_filePath);
            return;
        }

        var oldest = GetRotatedPath(_retainedFileCount - 1);
        File.Delete(oldest);
        for (var index = _retainedFileCount - 2; index >= 1; index--)
        {
            var source = GetRotatedPath(index);
            if (File.Exists(source))
            {
                File.Move(source, GetRotatedPath(index + 1));
            }
        }

        File.Move(_filePath, GetRotatedPath(1));
    }

    private string GetRotatedPath(int index) => Path.Combine(
        Path.GetDirectoryName(_filePath)!,
        $"{Path.GetFileNameWithoutExtension(_filePath)}.{index}{Path.GetExtension(_filePath)}");

    internal sealed record LogEntry(
        DateTimeOffset Timestamp,
        string Method,
        string Path,
        int StatusCode,
        long? ContentLength,
        double DurationMs)
    {
        public static LogEntry From(TransferRecord record)
        {
            var path = record.Path.Split('?', 2)[0];
            if (path.Length > MaximumLoggedPathLength || path.Any(char.IsControl))
            {
                path = "/";
            }

            var method = record.Method is "GET" or "HEAD" ? record.Method : "OTHER";
            return new(record.Timestamp, method, path, record.StatusCode, record.ContentLength,
                Math.Round(Math.Max(0, record.Duration.TotalMilliseconds), 3));
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JsonLinesTransferLogWriter.LogEntry))]
internal sealed partial class LogJsonContext : JsonSerializerContext;

internal sealed class NullTransferLogWriter : ITransferLogWriter
{
    public Exception? LastError => null;

    public bool TryWrite(TransferRecord record) => true;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
