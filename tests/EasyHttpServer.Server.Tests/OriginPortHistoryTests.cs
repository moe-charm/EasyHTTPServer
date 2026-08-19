using EasyHttpServer.Server;

namespace EasyHttpServer.Server.Tests;

public sealed class OriginPortHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "EasyHttpServerOriginHistoryTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateReserveAndRetirePreserveRoleBoundaries()
    {
        var store = CreateStore();
        store.Create(18080);
        store.ReserveFileSharingPort(19090);
        store.RetireWebsitePort(50000);

        var history = store.Load();

        Assert.Equal(OriginPortHistory.CurrentSchemaVersion, history.SchemaVersion);
        Assert.Contains(18080, history.FileShareReserved);
        Assert.Contains(19090, history.FileShareReserved);
        Assert.Contains(50000, history.WebsiteRetired);
        Assert.Throws<InvalidOperationException>(() => store.ReserveFileSharingPort(50000));
        Assert.Throws<InvalidOperationException>(() => store.RetireWebsitePort(19090));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void AllocatorReturnsFreshDynamicPrivatePortAndDurablyRetiresIt()
    {
        var store = CreateStore();
        store.Create(18080);
        var allocator = new OriginPortAllocator(store);

        var first = allocator.AllocateAndRetire();
        var second = allocator.AllocateAndRetire();

        Assert.InRange(first, OriginPortAllocator.MinimumPort, OriginPortAllocator.MaximumPort);
        Assert.InRange(second, OriginPortAllocator.MinimumPort, OriginPortAllocator.MaximumPort);
        Assert.NotEqual(first, second);
        var history = store.Load();
        Assert.Contains(first, history.WebsiteRetired);
        Assert.Contains(second, history.WebsiteRetired);
    }

    [Fact]
    public void CorruptOrOverlappingHistoryFailsClosed()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "history.json");
        File.WriteAllText(path, "{broken");
        var corrupt = new JsonOriginPortHistoryStore(path, UniqueMutexName());
        Assert.Throws<InvalidOperationException>(corrupt.Load);

        File.WriteAllText(path, """
            {"schemaVersion":1,"fileShareReserved":[50000],"websiteRetired":[50000]}
            """);
        var overlapping = new JsonOriginPortHistoryStore(path, UniqueMutexName());
        Assert.Throws<InvalidOperationException>(overlapping.Load);
    }

    private JsonOriginPortHistoryStore CreateStore() => new(
        Path.Combine(_root, "history.json"),
        UniqueMutexName());

    private static string UniqueMutexName() => $"Local\\EasyHttpServerOriginHistoryTests.{Guid.NewGuid():N}";

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
