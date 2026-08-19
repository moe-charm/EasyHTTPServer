using System.Text.Json;
using System.IO;
using EasyHttpServer.Core;
using EasyHttpServer.Desktop.Wpf;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "EasyHttpServerSettingsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RoundTripPreservesAllowlistedSettings()
    {
        var shareRoot = Directory.CreateDirectory(Path.Combine(_root, "docs")).FullName;
        var path = Path.Combine(_root, "settings.json");
        var store = new JsonSettingsStore(path);
        var share = ShareDefinition.Create("資料", "docs", shareRoot);

        await store.SaveAsync(new ApplicationSettings(1, 19090, true, [share]));
        var loaded = store.Load();

        Assert.Null(loaded.Warning);
        Assert.Equal(19090, loaded.Settings.Port);
        Assert.True(loaded.Settings.IsClassic);
        Assert.Equal(ApplicationSettings.CurrentSchemaVersion, loaded.Settings.SchemaVersion);
        Assert.Equal(ContentMode.FileSharing, loaded.Settings.ContentMode);
        Assert.Equal(share, Assert.Single(loaded.Settings.Shares));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void CorruptJsonIsPreservedAndDefaultsAreUsed()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        File.WriteAllText(path, "{broken");

        var loaded = new JsonSettingsStore(path).Load();

        Assert.Equal(ApplicationSettings.Default, loaded.Settings);
        Assert.NotNull(loaded.Warning);
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    [Fact]
    public void MissingAndDuplicateSharesAreNotRestored()
    {
        var validRoot = Directory.CreateDirectory(Path.Combine(_root, "valid")).FullName;
        var path = Path.Combine(_root, "settings.json");
        var document = new
        {
            schemaVersion = 1,
            port = 70000,
            isClassic = false,
            shares = new object[]
            {
                new { id = Guid.NewGuid(), name = "valid", slug = "valid", rootPath = validRoot, directoryBrowsingEnabled = true, preferIndexFile = true },
                new { id = Guid.NewGuid(), name = "duplicate", slug = "valid", rootPath = validRoot, directoryBrowsingEnabled = true, preferIndexFile = true },
                new { id = Guid.NewGuid(), name = "missing", slug = "missing", rootPath = Path.Combine(_root, "missing"), directoryBrowsingEnabled = true, preferIndexFile = true },
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(document));

        var loaded = new JsonSettingsStore(path).Load();

        Assert.Equal(18080, loaded.Settings.Port);
        Assert.Single(loaded.Settings.Shares);
        Assert.Contains("2件", loaded.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedSchemaUsesDefaultsWithoutOverwritingFile()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        const string json = "{\"schemaVersion\":99,\"port\":19090,\"isClassic\":true,\"shares\":[]}";
        File.WriteAllText(path, json);

        var loaded = new JsonSettingsStore(path).Load();

        Assert.Equal(ApplicationSettings.Default, loaded.Settings);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public async Task SchemaV2RoundTripPreservesBothModeConfigurations()
    {
        var shareRoot = Directory.CreateDirectory(Path.Combine(_root, "files")).FullName;
        var websiteRoot = Directory.CreateDirectory(Path.Combine(_root, "site")).FullName;
        await File.WriteAllTextAsync(Path.Combine(websiteRoot, "index.html"), "site");
        var path = Path.Combine(_root, "settings.json");
        var store = new JsonSettingsStore(path);
        var share = ShareDefinition.Create("files", "files", shareRoot);

        await store.SaveAsync(new ApplicationSettings(
            ApplicationSettings.CurrentSchemaVersion,
            18080,
            false,
            [share],
            ContentMode.Website,
            WebsiteDefinition.Create(websiteRoot)));
        var loaded = store.Load();

        Assert.Null(loaded.Warning);
        Assert.Equal(ContentMode.Website, loaded.Settings.ContentMode);
        Assert.Equal(websiteRoot, loaded.Settings.Website?.RootPath);
        Assert.Equal(share, Assert.Single(loaded.Settings.Shares));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(18080, document.RootElement.GetProperty("fileSharingPort").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("port", out _));
    }

    [Fact]
    public void StateInitializerCreatesHistoryForV1ButFailsClosedForV2HistoryLoss()
    {
        Directory.CreateDirectory(_root);
        var historyPath = Path.Combine(_root, "origin-port-history.json");
        var history = new JsonOriginPortHistoryStore(
            historyPath,
            $"Local\\EasyHttpServerSettingsTests.{Guid.NewGuid():N}");
        var v1 = new SettingsLoadResult(
            new ApplicationSettings(2, 19090, false, []),
            SourceSchemaVersion: 1);

        ApplicationStateInitializer.Initialize(v1, history);

        Assert.Contains(19090, history.Load().FileShareReserved);
        File.Delete(historyPath);
        var v2 = new SettingsLoadResult(
            ApplicationSettings.Default,
            SourceSchemaVersion: ApplicationSettings.CurrentSchemaVersion);
        Assert.Throws<InvalidOperationException>(() => ApplicationStateInitializer.Initialize(v2, history));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
