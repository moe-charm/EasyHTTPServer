using EasyHttpServer.Desktop.Wpf;
using System.IO;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class BundledGuideTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("easyhttpserver-guide-").FullName;

    [Fact]
    public void CreatesDirectoryBrowsingShareWhenTextGuideExists()
    {
        var guide = Directory.CreateDirectory(Path.Combine(_root, BundledGuide.DirectoryName)).FullName;
        File.WriteAllText(Path.Combine(guide, "README.txt"), "guide");

        var share = BundledGuide.TryCreate(_root);

        Assert.NotNull(share);
        Assert.Equal(BundledGuide.DisplayName, share.Name);
        Assert.Equal(BundledGuide.Slug, share.Slug);
        Assert.Equal(guide, share.RootPath);
        Assert.True(share.DirectoryBrowsingEnabled);
        Assert.False(share.PreferIndexFile);
    }

    [Fact]
    public void MissingGuideDoesNotCreateShare()
    {
        Assert.Null(BundledGuide.TryCreate(_root));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
