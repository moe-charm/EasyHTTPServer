using System.IO;
using System.Text.Json;
using EasyHttpServer.Desktop.Wpf;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class TransferLogWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "EasyHttpServerLogTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task WritesOnlyAllowlistedCamelCaseFieldsAndRemovesQuery()
    {
        var path = Path.Combine(_root, "transfers.jsonl");
        await using (var writer = new JsonLinesTransferLogWriter(path))
        {
            Assert.True(writer.TryWrite(CreateRecord("/s/docs/file.txt?token=secret")));
        }

        using var document = JsonDocument.Parse(Assert.Single(File.ReadAllLines(path)));
        var names = document.RootElement.EnumerateObject().Select(item => item.Name).ToArray();
        Assert.Equal(["timestamp", "method", "path", "statusCode", "contentLength", "durationMs"], names);
        Assert.Equal("/s/docs/file.txt", document.RootElement.GetProperty("path").GetString());
        Assert.DoesNotContain("secret", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RotatesBySizeAndKeepsConfiguredGenerationCount()
    {
        var path = Path.Combine(_root, "transfers.jsonl");
        await using (var writer = new JsonLinesTransferLogWriter(path, maximumFileBytes: 180, retainedFileCount: 3))
        {
            for (var index = 0; index < 12; index++)
            {
                writer.TryWrite(CreateRecord($"/s/docs/{index:D2}.txt"));
            }
        }

        Assert.True(File.Exists(path));
        Assert.True(File.Exists(Path.Combine(_root, "transfers.1.jsonl")));
        Assert.True(File.Exists(Path.Combine(_root, "transfers.2.jsonl")));
        Assert.False(File.Exists(Path.Combine(_root, "transfers.3.jsonl")));
        Assert.Equal(3, Directory.GetFiles(_root, "*.jsonl").Length);
        foreach (var line in Directory.GetFiles(_root, "*.jsonl").SelectMany(File.ReadAllLines))
        {
            using var _ = JsonDocument.Parse(line);
        }
    }

    [Fact]
    public async Task ControlCharactersAndUnknownMethodsAreSanitized()
    {
        var path = Path.Combine(_root, "transfers.jsonl");
        await using (var writer = new JsonLinesTransferLogWriter(path))
        {
            writer.TryWrite(CreateRecord("/s/docs/bad\r\nname", "POST"));
        }

        using var document = JsonDocument.Parse(Assert.Single(File.ReadAllLines(path)));
        Assert.Equal("/", document.RootElement.GetProperty("path").GetString());
        Assert.Equal("OTHER", document.RootElement.GetProperty("method").GetString());
    }

    [Fact]
    public async Task ConcurrentProducersProduceValidLines()
    {
        var path = Path.Combine(_root, "transfers.jsonl");
        await using (var writer = new JsonLinesTransferLogWriter(path, queueCapacity: 512))
        {
            await Task.WhenAll(Enumerable.Range(0, 200).Select(index => Task.Run(() =>
                writer.TryWrite(CreateRecord($"/s/docs/{index}.txt")))));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(200, lines.Length);
        foreach (var line in lines)
        {
            using var _ = JsonDocument.Parse(line);
        }
    }

    [Fact]
    public async Task WriteFailureIsReportedWithoutEscapingDispose()
    {
        var directoryAsFile = Directory.CreateDirectory(Path.Combine(_root, "not-a-file")).FullName;
        var writer = new JsonLinesTransferLogWriter(directoryAsFile);

        Assert.True(writer.TryWrite(CreateRecord("/s/docs/file.txt")));
        await writer.DisposeAsync();

        Assert.NotNull(writer.LastError);
        Assert.False(writer.TryWrite(CreateRecord("/after-dispose")));
    }

    private static TransferRecord CreateRecord(string path, string method = "GET") => new(
        new DateTimeOffset(2026, 8, 19, 12, 34, 56, TimeSpan.FromHours(9)),
        method, path, 200, 123, TimeSpan.FromMilliseconds(4.5678));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
