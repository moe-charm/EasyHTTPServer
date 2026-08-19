using System.Diagnostics;
using EasyHttpServer.Core;

namespace EasyHttpServer.Server.Tests;

public sealed class SharePathResolverTests : IDisposable
{
    private readonly TemporaryDirectory _sandbox = new();
    private readonly string _root;
    private readonly ShareDefinition _share;
    private readonly SharePathResolver _resolver = new();

    public SharePathResolverTests()
    {
        _root = System.IO.Path.Combine(_sandbox.Path, "share");
        Directory.CreateDirectory(System.IO.Path.Combine(_root, "folder"));
        File.WriteAllText(System.IO.Path.Combine(_root, "folder", "hello.txt"), "hello");
        File.WriteAllText(System.IO.Path.Combine(_root, "a%20b.txt"), "percent");
        File.WriteAllText(System.IO.Path.Combine(_root, "a b.txt"), "space");
        _share = ShareDefinition.Create("Test", "test", _root);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("%2e%2e/secret.txt")]
    [InlineData("%252e%252e/secret.txt")]
    [InlineData("folder%2fsecret.txt")]
    [InlineData("folder%5csecret.txt")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("//server/share/file.txt")]
    [InlineData("file.txt:stream")]
    [InlineData("file%00.txt")]
    [InlineData("name.")]
    [InlineData("name%20")]
    [InlineData("CON")]
    [InlineData("CON .txt")]
    [InlineData("nul.txt")]
    [InlineData("folder//hello.txt")]
    public void ResolveRejectsHostileInput(string relativePath)
    {
        var result = _resolver.Resolve(_share, relativePath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Status, new[] { PathResolutionStatus.InvalidPath, PathResolutionStatus.OutsideRoot });
    }

    [Fact]
    public void ResolveReturnsExistingFileInsideShare()
    {
        var result = _resolver.Resolve(_share, "folder/hello.txt");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsDirectory);
        Assert.Equal(System.IO.Path.Combine(_root, "folder", "hello.txt"), result.FullPath);
    }

    [Fact]
    public void ResolvePreservesLiteralPercentInRouteDecodedFileName()
    {
        var result = _resolver.Resolve(_share, "a%20b.txt");

        Assert.True(result.IsSuccess);
        Assert.Equal(System.IO.Path.Combine(_root, "a%20b.txt"), result.FullPath);
    }

    [Fact]
    public void ResolveDoesNotUseShareNamePrefixAsBoundary()
    {
        var sibling = _root + "-private";
        Directory.CreateDirectory(sibling);
        File.WriteAllText(System.IO.Path.Combine(sibling, "secret.txt"), "secret");

        var result = _resolver.Resolve(_share, "../share-private/secret.txt");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ResolveRejectsSymbolicLinkThatLeavesShare()
    {
        var outside = System.IO.Path.Combine(_sandbox.Path, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "secret");
        var link = System.IO.Path.Combine(_root, "outside-link");

        Directory.CreateSymbolicLink(link, outside);

        var result = _resolver.Resolve(_share, "outside-link/secret.txt");

        Assert.Equal(PathResolutionStatus.ReparsePoint, result.Status);
    }

    [Fact]
    public void ResolveRejectsJunctionThatLeavesShare()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = System.IO.Path.Combine(_sandbox.Path, "junction-outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "secret");
        var junction = System.IO.Path.Combine(_root, "outside-junction");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList = { "/d", "/c", "mklink", "/J", junction, outside },
        });
        Assert.NotNull(process);
        process.WaitForExit();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.True(process.ExitCode == 0, output);

        try
        {
            var result = _resolver.Resolve(_share, "outside-junction/secret.txt");

            Assert.Equal(PathResolutionStatus.ReparsePoint, result.Status);
        }
        finally
        {
            if (Directory.Exists(junction))
            {
                Directory.Delete(junction);
            }
        }
    }

    public void Dispose() => _sandbox.Dispose();
}
