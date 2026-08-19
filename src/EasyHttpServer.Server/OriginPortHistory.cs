using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace EasyHttpServer.Server;

public sealed record OriginPortHistory(
    int SchemaVersion,
    IReadOnlySet<int> FileShareReserved,
    IReadOnlySet<int> WebsiteRetired)
{
    public const int CurrentSchemaVersion = 1;
}

public interface IOriginPortHistoryStore
{
    bool Exists { get; }

    OriginPortHistory Load();

    void Create(int fileSharingPort);

    void ReserveFileSharingPort(int port);

    void RetireWebsitePort(int port);

    int AllocateAndRetireWebsitePort(Func<OriginPortHistory, int> selector);
}

public sealed class JsonOriginPortHistoryStore(string filePath, string? mutexName = null) : IOriginPortHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _mutexName = mutexName ?? @"Global\charmpic.EasyHTTPServer2.OriginPortHistoryLock";

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EasyHTTPServer",
        "origin-port-history.json");

    public bool Exists => File.Exists(filePath);

    public OriginPortHistory Load() => WithLock(ReadValidated);

    public void Create(int fileSharingPort)
    {
        ValidatePort(fileSharingPort);
        WithLock(() =>
        {
            if (File.Exists(filePath))
            {
                throw new InvalidOperationException("Origin port history already exists.");
            }

            WriteDurable(new OriginPortHistory(
                OriginPortHistory.CurrentSchemaVersion,
                new HashSet<int> { fileSharingPort },
                new HashSet<int>()));
            return 0;
        });
    }

    public void ReserveFileSharingPort(int port)
    {
        ValidatePort(port);
        WithLock(() =>
        {
            var current = ReadValidated();
            if (current.WebsiteRetired.Contains(port))
            {
                throw new InvalidOperationException("A retired website origin cannot be used for file sharing.");
            }

            if (!current.FileShareReserved.Contains(port))
            {
                WriteDurable(current with
                {
                    FileShareReserved = current.FileShareReserved.Append(port).ToHashSet(),
                });
            }

            return 0;
        });
    }

    public void RetireWebsitePort(int port)
    {
        ValidatePort(port);
        WithLock(() =>
        {
            var current = ReadValidated();
            if (current.FileShareReserved.Contains(port) || current.WebsiteRetired.Contains(port))
            {
                throw new InvalidOperationException("The website port is not fresh.");
            }

            WriteDurable(current with
            {
                WebsiteRetired = current.WebsiteRetired.Append(port).ToHashSet(),
            });
            return 0;
        });
    }

    public int AllocateAndRetireWebsitePort(Func<OriginPortHistory, int> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return WithLock(() =>
        {
            var current = ReadValidated();
            var candidate = selector(current);
            ValidatePort(candidate);
            if (current.FileShareReserved.Contains(candidate) || current.WebsiteRetired.Contains(candidate))
            {
                throw new InvalidOperationException("The website port selector did not return a fresh port.");
            }

            WriteDurable(current with
            {
                WebsiteRetired = current.WebsiteRetired.Append(candidate).ToHashSet(),
            });
            return candidate;
        });
    }

    private T WithLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, _mutexName);
        var owns = false;
        try
        {
            try
            {
                owns = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                owns = true;
            }

            if (!owns)
            {
                throw new TimeoutException("Origin port history is locked by another process.");
            }

            return action();
        }
        finally
        {
            if (owns)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private OriginPortHistory ReadValidated()
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("Origin port history is missing.");
        }

        try
        {
            var document = JsonSerializer.Deserialize<HistoryDocument>(File.ReadAllText(filePath), JsonOptions);
            if (document is null || document.SchemaVersion != OriginPortHistory.CurrentSchemaVersion)
            {
                throw new InvalidOperationException("Origin port history has an unsupported schema.");
            }

            var filePorts = (document.FileShareReserved ?? []).ToHashSet();
            var websitePorts = (document.WebsiteRetired ?? []).ToHashSet();
            if (filePorts.Any(port => port is <= 0 or > IPEndPoint.MaxPort) ||
                websitePorts.Any(port => port is <= 0 or > IPEndPoint.MaxPort) ||
                filePorts.Overlaps(websitePorts))
            {
                throw new InvalidOperationException("Origin port history is inconsistent.");
            }

            return new(document.SchemaVersion, filePorts, websitePorts);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Origin port history is corrupt.", exception);
        }
    }

    private void WriteDurable(OriginPortHistory history)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new InvalidOperationException("Origin history path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var document = new HistoryDocument(
                history.SchemaVersion,
                history.FileShareReserved.Order().ToArray(),
                history.WebsiteRetired.Order().ToArray());
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, document, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidatePort(int port)
    {
        if (port is <= 0 or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
    }

    private sealed record HistoryDocument(
        int SchemaVersion,
        IReadOnlyList<int>? FileShareReserved,
        IReadOnlyList<int>? WebsiteRetired);
}

public interface IOriginPortAllocator
{
    int AllocateAndRetire();
}

public sealed class OriginPortAllocator(IOriginPortHistoryStore historyStore) : IOriginPortAllocator
{
    public const int MinimumPort = 49152;
    public const int MaximumPort = 65535;
    public const int MaximumBindAttempts = 32;

    public int AllocateAndRetire()
    {
        return historyStore.AllocateAndRetireWebsitePort(history =>
        {
            var unavailable = history.FileShareReserved.Concat(history.WebsiteRetired).ToHashSet();
            var availableCount = MaximumPort - MinimumPort + 1 - unavailable.Count(port => port is >= MinimumPort and <= MaximumPort);
            if (availableCount <= 0)
            {
                throw new InvalidOperationException("No fresh website port remains.");
            }

            var start = RandomNumberGenerator.GetInt32(MinimumPort, MaximumPort + 1);
            for (var offset = 0; offset <= MaximumPort - MinimumPort; offset++)
            {
                var candidate = MinimumPort + ((start - MinimumPort + offset) % (MaximumPort - MinimumPort + 1));
                if (!unavailable.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No fresh website port remains.");
        });
    }

}
