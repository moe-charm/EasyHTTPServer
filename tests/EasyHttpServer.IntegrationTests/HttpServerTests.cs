using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using EasyHttpServer.Core;
using EasyHttpServer.Server;

namespace EasyHttpServer.IntegrationTests;

public sealed class HttpServerTests : IAsyncLifetime, IDisposable
{
    private readonly string _sandboxPath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "EasyHttpServerIntegrationTests",
        Guid.NewGuid().ToString("N"));
    private ServerController? _server;
    private HttpClient? _client;
    private ShareDefinition? _share;
    private byte[] _fileContents = [];

    public async Task InitializeAsync()
    {
        var sharePath = System.IO.Path.Combine(_sandboxPath, "public");
        Directory.CreateDirectory(sharePath);
        _fileContents = Enumerable.Range(0, 4096).Select(index => (byte)(index % 251)).ToArray();
        await File.WriteAllBytesAsync(System.IO.Path.Combine(sharePath, "sample.bin"), _fileContents);
        await File.WriteAllTextAsync(System.IO.Path.Combine(sharePath, "hello.txt"), "hello world");
        await File.WriteAllTextAsync(System.IO.Path.Combine(sharePath, "rock&roll.txt"), "safe");
        await File.WriteAllTextAsync(System.IO.Path.Combine(sharePath, "a%20b.txt"), "percent");
        await File.WriteAllTextAsync(System.IO.Path.Combine(sharePath, "a b.txt"), "space");
        var nestedPath = Directory.CreateDirectory(System.IO.Path.Combine(sharePath, "日本語", "empty")).Parent!.FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(nestedPath, "inside.txt"), "nested");

        _server = new ServerController(new SharePathResolver());
        _share = ShareDefinition.Create("Public <Files>", "public", sharePath);
        await _server.StartAsync(ServerOptions.Loopback(0, [_share]));

        _client = new HttpClient
        {
            BaseAddress = Assert.Single(_server.BaseAddresses),
        };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _client = null;
        var server = _server;
        _server = null;
        if (server is not null)
        {
            await server.DisposeAsync();
        }

        if (Directory.Exists(_sandboxPath))
        {
            Directory.Delete(_sandboxPath, recursive: true);
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetStreamsExactFileContents()
    {
        var response = await Client.GetAsync("/s/public/sample.bin");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(_fileContents, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task HeadReturnsHeadersWithoutBody()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "/s/public/hello.txt");
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(11, response.Content.Headers.ContentLength);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task RangeReturnsRequestedBytes()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/s/public/sample.bin");
        request.Headers.Range = new RangeHeaderValue(100, 199);
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 100-199/4096", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(_fileContents[100..200], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task SuffixRangeReturnsLastBytes()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/s/public/sample.bin");
        request.Headers.TryAddWithoutValidation("Range", "bytes=-16");
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(_fileContents[^16..], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnsatisfiableRangeReturnsRequestedRangeNotSatisfiable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/s/public/sample.bin");
        request.Headers.Range = new RangeHeaderValue(99999, 100000);
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
    }

    [Fact]
    public async Task PostToFileRouteReturnsMethodNotAllowed()
    {
        var response = await Client.PostAsync("/s/public/hello.txt", content: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task NonLoopbackHostHeaderIsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/s/public/hello.txt");
        request.Headers.Host = "attacker.example";

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/s/public/%252e%252e/secret.txt")]
    [InlineData("/s/public/folder%255csecret.txt")]
    [InlineData("/s/public/file.txt%253astream")]
    public async Task EncodedBoundaryAttacksAreRejected(string path)
    {
        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PercentEncodedLiteralPercentFileNameResolvesWithoutDoubleDecoding()
    {
        var response = await Client.GetAsync("/s/public/a%2520b.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("percent", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DirectoryListingEncodesNamesAndAddsSecurityHeaders()
    {
        var response = await Client.GetAsync("/s/public/");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("rock&amp;roll.txt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rock&roll.txt", html, StringComparison.Ordinal);
        Assert.Contains("rock%26roll.txt", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task NestedDirectoryLinksWithTrailingSlashRemainBrowsable()
    {
        var nested = await Client.GetAsync("/s/public/%E6%97%A5%E6%9C%AC%E8%AA%9E/");
        var nestedHtml = await nested.Content.ReadAsStringAsync();
        var empty = await Client.GetAsync("/s/public/%E6%97%A5%E6%9C%AC%E8%AA%9E/empty/");

        nested.EnsureSuccessStatusCode();
        empty.EnsureSuccessStatusCode();
        Assert.Contains("inside.txt", nestedHtml, StringComparison.Ordinal);
        Assert.Contains("/s/public/", nestedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoubleSlashInsideDirectoryPathRemainsForbidden()
    {
        var response = await Client.GetAsync("/s/public/%E6%97%A5%E6%9C%AC%E8%AA%9E//");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownExtensionIsDownloaded()
    {
        var unknownPath = System.IO.Path.Combine(_sandboxPath, "public", "archive.unknown-extension");
        await File.WriteAllTextAsync(unknownPath, "content");

        var response = await Client.GetAsync("/s/public/archive.unknown-extension");

        response.EnsureSuccessStatusCode();
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("archive.unknown-extension", response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Theory]
    [InlineData("page.html", "text/html")]
    [InlineData("image.svg", "image/svg+xml")]
    [InlineData("module.js", "text/javascript")]
    [InlineData("data.xml", "text/xml")]
    public async Task ActiveWebContentIsAlwaysDownloaded(string fileName, string expectedMediaType)
    {
        var path = System.IO.Path.Combine(_sandboxPath, "public", fileName);
        await File.WriteAllTextAsync(path, "<script>throw new Error('must not execute')</script>");

        var response = await Client.GetAsync($"/s/public/{fileName}");

        response.EnsureSuccessStatusCode();
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(fileName, response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task FileSharingIndexRemainsDownloadOnlyAndKeepsStrictHeaders()
    {
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(_sandboxPath, "public", "index.html"),
            "<!doctype html><title>file sharing</title>");

        using var response = await Client.GetAsync("/s/public/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("default-src 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Cross-Origin-Resource-Policy")));
    }

    [Fact]
    public async Task WebsiteModeServesIndexAndStaticAssetsInlineFromRoot()
    {
        var websitePath = System.IO.Path.Combine(_sandboxPath, "website-inline");
        Directory.CreateDirectory(websitePath);
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "index.html"),
            "<!doctype html><link rel=\"stylesheet\" href=\"site.css\"><script src=\"site.js\"></script><h1>教材</h1>");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "site.css"), "h1{color:navy}");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "site.js"), "document.body.dataset.ready='yes';");

        await using var server = new ServerController(new SharePathResolver());
        await server.StartAsync(ServerOptions.WebsiteLoopback(0, WebsiteDefinition.Create(websitePath)));
        using var client = new HttpClient { BaseAddress = Assert.Single(server.BaseAddresses) };

        using var index = await client.GetAsync("/");
        index.EnsureSuccessStatusCode();
        Assert.Equal("text/html", index.Content.Headers.ContentType?.MediaType);
        Assert.Null(index.Content.Headers.ContentDisposition);
        Assert.Contains("教材", await index.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("worker-src 'none'", Assert.Single(index.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.Equal("SAMEORIGIN", Assert.Single(index.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("same-origin", Assert.Single(index.Headers.GetValues("Cross-Origin-Resource-Policy")));

        using var css = await client.GetAsync("/site.css");
        css.EnsureSuccessStatusCode();
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        Assert.Null(css.Content.Headers.ContentDisposition);

        using var script = await client.GetAsync("/site.js");
        script.EnsureSuccessStatusCode();
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.Null(script.Content.Headers.ContentDisposition);

        using var fileShareRoute = await client.GetAsync("/s/public/hello.txt");
        Assert.Equal(HttpStatusCode.NotFound, fileShareRoute.StatusCode);
    }

    [Fact]
    public async Task WebsiteModeUsesCanonicalDirectoryUrlsAndNeverListsDirectories()
    {
        var websitePath = System.IO.Path.Combine(_sandboxPath, "website-directories");
        var nestedPath = Directory.CreateDirectory(System.IO.Path.Combine(websitePath, "lesson")).FullName;
        Directory.CreateDirectory(System.IO.Path.Combine(websitePath, "empty"));
        var htmPath = Directory.CreateDirectory(System.IO.Path.Combine(websitePath, "legacy")).FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "index.html"), "root");
        await File.WriteAllTextAsync(System.IO.Path.Combine(nestedPath, "index.html"), "nested");
        await File.WriteAllTextAsync(System.IO.Path.Combine(htmPath, "index.htm"), "legacy");

        await using var server = new ServerController(new SharePathResolver());
        await server.StartAsync(ServerOptions.WebsiteLoopback(0, WebsiteDefinition.Create(websitePath)));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = Assert.Single(server.BaseAddresses) };

        using var redirect = await client.GetAsync("/lesson");
        Assert.Equal(HttpStatusCode.PermanentRedirect, redirect.StatusCode);
        Assert.Equal("/lesson/", redirect.Headers.Location?.OriginalString);

        using var nested = await client.GetAsync("/lesson/");
        nested.EnsureSuccessStatusCode();
        Assert.Equal("nested", await nested.Content.ReadAsStringAsync());

        using var legacy = await client.GetAsync("/legacy/");
        legacy.EnsureSuccessStatusCode();
        Assert.Equal("legacy", await legacy.Content.ReadAsStringAsync());

        using var empty = await client.GetAsync("/empty/");
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.DoesNotContain("Index of", await empty.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebsiteModeRejectsSecretCandidatesAndDownloadsUnknownTypes()
    {
        var websitePath = Directory.CreateDirectory(System.IO.Path.Combine(_sandboxPath, "website-policy")).FullName;
        Directory.CreateDirectory(System.IO.Path.Combine(websitePath, ".git"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "index.html"), "root");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, ".git", "config"), "secret");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "appsettings.json"), "{}");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "server.key"), "secret");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "asset.custom"), "download me");

        await using var server = new ServerController(new SharePathResolver());
        await server.StartAsync(ServerOptions.WebsiteLoopback(0, WebsiteDefinition.Create(websitePath)));
        using var client = new HttpClient { BaseAddress = Assert.Single(server.BaseAddresses) };

        foreach (var path in new[] { "/.git/config", "/appsettings.json", "/server.key" })
        {
            using var rejected = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        }

        using var unknown = await client.GetAsync("/asset.custom");
        unknown.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", unknown.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", unknown.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task WebsiteHandlesPreventReplacementWhilePublishedAndReleaseOnStop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var websitePath = Directory.CreateDirectory(System.IO.Path.Combine(_sandboxPath, "website-handles")).FullName;
        var movedPath = System.IO.Path.Combine(_sandboxPath, "website-handles-moved");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "index.html"), "root");

        await using var server = new ServerController(new SharePathResolver());
        await server.StartAsync(ServerOptions.WebsiteLoopback(0, WebsiteDefinition.Create(websitePath)));

        Assert.ThrowsAny<IOException>(() => Directory.Move(websitePath, movedPath));

        await server.StopAsync();
        Directory.Move(websitePath, movedPath);
        Assert.True(Directory.Exists(movedPath));
    }

    [Fact]
    public async Task WebsiteRejectsCrossSiteSubresourcesButAllowsUserNavigation()
    {
        var websitePath = Directory.CreateDirectory(System.IO.Path.Combine(_sandboxPath, "website-fetch-metadata")).FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "index.html"), "root");

        await using var server = new ServerController(new SharePathResolver());
        await server.StartAsync(ServerOptions.WebsiteLoopback(0, WebsiteDefinition.Create(websitePath)));
        using var client = new HttpClient { BaseAddress = Assert.Single(server.BaseAddresses) };

        using var subresourceRequest = new HttpRequestMessage(HttpMethod.Get, "/index.html");
        subresourceRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");
        subresourceRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
        subresourceRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "script");
        using var rejected = await client.SendAsync(subresourceRequest);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        using var navigationRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        navigationRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");
        navigationRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        navigationRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        navigationRequest.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        using var allowed = await client.SendAsync(navigationRequest);
        allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task WebsitePreservesLegacyBytesAndSupportsRangeFromVerifiedHandle()
    {
        var websitePath = Directory.CreateDirectory(System.IO.Path.Combine(_sandboxPath, "website-bytes")).FullName;
        var bytes = new byte[] { 0x3C, 0x6D, 0x65, 0x74, 0x61, 0x20, 0x63, 0x68, 0x61, 0x72, 0x73, 0x65, 0x74, 0x3D, 0x53, 0x68, 0x69, 0x66, 0x74, 0x5F, 0x4A, 0x49, 0x53, 0x3E, 0x82, 0xA0, 0x82, 0xA2 };
        await File.WriteAllBytesAsync(System.IO.Path.Combine(websitePath, "index.html"), bytes);

        await using var server = new ServerController(new SharePathResolver());
        await server.StartAsync(ServerOptions.WebsiteLoopback(0, WebsiteDefinition.Create(websitePath)));
        using var client = new HttpClient { BaseAddress = Assert.Single(server.BaseAddresses) };

        using var full = await client.GetAsync("/");
        full.EnsureSuccessStatusCode();
        Assert.Equal(bytes, await full.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("charset", full.Content.Headers.ContentType?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, "/index.html");
        rangeRequest.Headers.Range = new RangeHeaderValue(24, 27);
        using var partial = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(bytes[24..28], await partial.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task LargeSparseFileSupportsSuffixRangeWithoutBufferingWholeFile()
    {
        const long fileLength = 10L * 1024 * 1024 * 1024;
        var largePath = System.IO.Path.Combine(_sandboxPath, "public", "large.bin");
        var marker = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        await using (var stream = new FileStream(
                         largePath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.Read,
                         bufferSize: 4096,
                         FileOptions.Asynchronous))
        {
            stream.SetLength(fileLength);
            stream.Position = fileLength - marker.Length;
            await stream.WriteAsync(marker);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/s/public/large.bin");
        request.Headers.TryAddWithoutValidation("Range", $"bytes=-{marker.Length}");
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(marker, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal($"bytes {fileLength - marker.Length}-{fileLength - 1}/{fileLength}", response.Content.Headers.ContentRange?.ToString());
    }

    [Fact]
    public async Task CompletedRequestRaisesTransferEvent()
    {
        var completion = new TaskCompletionSource<TransferRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnTransferCompleted(object? sender, TransferRecord record) => completion.TrySetResult(record);

        Assert.NotNull(_server);
        _server.TransferCompleted += OnTransferCompleted;
        try
        {
            var response = await Client.GetAsync("/s/public/hello.txt");
            response.EnsureSuccessStatusCode();
            var record = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal("GET", record.Method);
            Assert.Equal("/s/public/hello.txt", record.Path);
            Assert.Equal(200, record.StatusCode);
        }
        finally
        {
            _server.TransferCompleted -= OnTransferCompleted;
        }
    }

    [Fact]
    public async Task ServerCanRestartAfterGracefulStop()
    {
        Assert.NotNull(_server);
        Assert.NotNull(_share);
        await _server.StopAsync();
        await _server.StartAsync(ServerOptions.Loopback(0, [_share]));
        using var restartedClient = new HttpClient { BaseAddress = Assert.Single(_server.BaseAddresses) };

        var response = await restartedClient.GetAsync("/s/public/hello.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("hello world", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FiftyParallelStreamingDownloadsCompleteWithExactContents()
    {
        var loadBytes = new byte[1024 * 1024];
        new Random(20260819).NextBytes(loadBytes);
        await File.WriteAllBytesAsync(System.IO.Path.Combine(_sandboxPath, "public", "load.bin"), loadBytes);
        var expectedHash = SHA256.HashData(loadBytes);
        var completed = 0;
        var allCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnTransferCompleted(object? sender, TransferRecord record)
        {
            if (record.Path == "/s/public/load.bin" && Interlocked.Increment(ref completed) == 50)
            {
                allCompleted.TrySetResult();
            }
        }

        Assert.NotNull(_server);
        _server.TransferCompleted += OnTransferCompleted;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var downloads = Enumerable.Range(0, 50).Select(async _ =>
            {
                using var response = await Client.GetAsync(
                    "/s/public/load.bin", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                return await SHA256.HashDataAsync(stream, timeout.Token);
            });

            var hashes = await Task.WhenAll(downloads).WaitAsync(timeout.Token);
            await allCompleted.Task.WaitAsync(timeout.Token);
            Assert.All(hashes, hash => Assert.Equal(expectedHash, hash));
            Assert.Equal(50, Volatile.Read(ref completed));
        }
        finally
        {
            _server.TransferCompleted -= OnTransferCompleted;
        }
    }

    [Fact]
    public async Task IncompleteHeadersAreTerminatedWithinConfiguredDeadline()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ServerPort);
        await using var stream = client.GetStream();
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: localhost\r\nX-Slow:"u8.ToArray());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var response = await ReadUntilClosedAsync(stream, timeout.Token);

        Assert.True(response.Length == 0 || Encoding.ASCII.GetString(response).Contains(" 408 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConnectionLimitRejectsOverflowAndRecoversAfterRelease()
    {
        var blockers = await Task.WhenAll(Enumerable.Range(0, 100).Select(async _ =>
        {
            var socket = new TcpClient();
            await socket.ConnectAsync(IPAddress.Loopback, ServerPort);
            await socket.GetStream().WriteAsync("GET / HTTP/1.1\r\nHost: localhost\r\nX-Hold:"u8.ToArray());
            return socket;
        }));

        try
        {
            await Task.Delay(250);
            using var overflow = new TcpClient();
            await overflow.ConnectAsync(IPAddress.Loopback, ServerPort);
            await using var overflowStream = overflow.GetStream();
            await overflowStream.WriteAsync("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n"u8.ToArray());
            using var rejectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var overflowResponse = await ReadUntilClosedAsync(overflowStream, rejectionTimeout.Token);
            Assert.DoesNotContain(" 200 ", Encoding.ASCII.GetString(overflowResponse), StringComparison.Ordinal);
        }
        finally
        {
            foreach (var blocker in blockers)
            {
                blocker.Dispose();
            }
        }

        using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        HttpResponseMessage? recovered = null;
        while (!recoveryTimeout.IsCancellationRequested)
        {
            try
            {
                recovered = await Client.GetAsync("/s/public/hello.txt", recoveryTimeout.Token);
                if (recovered.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
            }

            recovered?.Dispose();
            recovered = null;
            await Task.Delay(50, recoveryTimeout.Token);
        }

        using (recovered)
        {
            Assert.NotNull(recovered);
            recovered.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task LanHttpsRequiresAccessCodeAndRejectsDifferentHost()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(LanNetworkAddress.IsPrivateIpv4);
        if (address is null)
        {
            return;
        }

        Assert.NotNull(_share);
        using var security = LanSecurityMaterial.Create(address);
        await using var lanServer = new ServerController(new SharePathResolver());
        var sharedPort = ReserveAvailableLoopbackPort();
        await lanServer.StartAsync(ServerOptions.Lan(
            address, sharedPort, [_share], security.Certificate, security.AccessCode));
        using var handler = new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && Convert.ToHexString(SHA256.HashData(certificate.RawData)) == security.Fingerprint,
        };
        Assert.Equal(2, lanServer.BaseAddresses.Count);
        var loopbackAddress = Assert.Single(lanServer.BaseAddresses, item => item.Scheme == Uri.UriSchemeHttp);
        var lanAddress = Assert.Single(lanServer.BaseAddresses, item => item.Scheme == Uri.UriSchemeHttps);
        Assert.Equal(sharedPort, loopbackAddress.Port);
        Assert.Equal(sharedPort, lanAddress.Port);
        Assert.True(IPAddress.IsLoopback(IPAddress.Parse(loopbackAddress.Host)));
        Assert.Equal(address, IPAddress.Parse(lanAddress.Host));
        using var client = new HttpClient(handler) { BaseAddress = lanAddress };

        using var loopbackClient = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = loopbackAddress,
        };
        using var local = await loopbackClient.GetAsync("/s/public/hello.txt");
        local.EnsureSuccessStatusCode();
        Assert.Empty(local.Headers.WwwAuthenticate);
        Assert.Equal("hello world", await local.Content.ReadAsStringAsync());

        using var unauthorized = await client.GetAsync("/s/public/hello.txt");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("Basic", Assert.Single(unauthorized.Headers.WwwAuthenticate).Scheme);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("easyhttp:WRONG")));
        using var wrong = await client.GetAsync("/s/public/hello.txt");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"easyhttp:{security.AccessCode}")));
        using var allowed = await client.GetAsync("/s/public/hello.txt");
        allowed.EnsureSuccessStatusCode();
        Assert.Equal("hello world", await allowed.Content.ReadAsStringAsync());

        using var spoofedRequest = new HttpRequestMessage(HttpMethod.Get, "/s/public/hello.txt");
        spoofedRequest.Headers.Host = "attacker.example";
        using var spoofed = await client.SendAsync(spoofedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);

        using var loopbackSpoof = new HttpRequestMessage(HttpMethod.Get, "/s/public/hello.txt");
        loopbackSpoof.Headers.Host = address.ToString();
        using var loopbackSpoofed = await loopbackClient.SendAsync(loopbackSpoof);
        Assert.Equal(HttpStatusCode.BadRequest, loopbackSpoofed.StatusCode);
    }

    [Fact]
    public async Task LanPairingIssuesSecureSessionCookieWithoutPuttingSecretInUrl()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(LanNetworkAddress.IsPrivateIpv4);
        if (address is null)
        {
            return;
        }

        Assert.NotNull(_share);
        using var security = LanSecurityMaterial.Create(address);
        using var pairing = new LanPairingSession();
        await using var lanServer = new ServerController(new SharePathResolver());
        await lanServer.StartAsync(ServerOptions.LanWithPairing(
            address, 0, [_share], security.Certificate, pairing));
        var lanAddress = Assert.Single(lanServer.BaseAddresses, item => item.Scheme == Uri.UriSchemeHttps);
        using var handler = new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        using var client = new HttpClient(handler) { BaseAddress = lanAddress };

        using var unauthorized = await client.GetAsync("/s/public/hello.txt");
        Assert.Equal(HttpStatusCode.Redirect, unauthorized.StatusCode);
        Assert.Equal("/_easyhttp/pair", unauthorized.Headers.Location?.OriginalString);

        using var page = await client.GetAsync("/_easyhttp/pair");
        page.EnsureSuccessStatusCode();
        Assert.Equal("no-store", page.Headers.CacheControl?.ToString());
        Assert.Contains("form-action 'self'", Assert.Single(page.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.DoesNotContain(pairing.Code, await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var wrongCode = pairing.Code == "00000000" ? "11111111" : "00000000";
        using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, "/_easyhttp/pair")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = wrongCode }),
        };
        wrongRequest.Headers.TryAddWithoutValidation("Origin", lanAddress.GetLeftPart(UriPartial.Authority));
        using var wrong = await client.SendAsync(wrongRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        using var pairRequest = new HttpRequestMessage(HttpMethod.Post, "/_easyhttp/pair")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = pairing.Code }),
        };
        pairRequest.Headers.TryAddWithoutValidation("Origin", lanAddress.GetLeftPart(UriPartial.Authority));
        using var paired = await client.SendAsync(pairRequest);
        Assert.Equal(HttpStatusCode.Redirect, paired.StatusCode);
        Assert.Equal("/", paired.Headers.Location?.OriginalString);
        var setCookie = Assert.Single(paired.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-EasyHttpFilesSession=", setCookie, StringComparison.Ordinal);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);

        using var allowed = await client.GetAsync("/s/public/hello.txt");
        allowed.EnsureSuccessStatusCode();
        Assert.Equal("hello world", await allowed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VpnModeBindsExactVirtualAdapterAddressWithPairing()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up &&
                           (item.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp ||
                            (int)item.NetworkInterfaceType == 53))
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(VpnNetworkAddress.IsAllowedIpv4);
        if (address is null)
        {
            return;
        }

        Assert.NotNull(_share);
        using var security = LanSecurityMaterial.Create(address);
        using var pairing = new LanPairingSession();
        await using var vpnServer = new ServerController(new SharePathResolver());
        await vpnServer.StartAsync(ServerOptions.VpnWithPairing(
            address, 0, [_share], security.Certificate, pairing));
        var vpnAddress = Assert.Single(vpnServer.BaseAddresses, item => item.Scheme == Uri.UriSchemeHttps);
        var loopbackAddress = Assert.Single(vpnServer.BaseAddresses, item => item.Scheme == Uri.UriSchemeHttp);

        Assert.Equal(address, IPAddress.Parse(vpnAddress.Host));
        Assert.True(IPAddress.IsLoopback(IPAddress.Parse(loopbackAddress.Host)));
        using var handler = new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        using var client = new HttpClient(handler) { BaseAddress = vpnAddress };
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/_easyhttp/pair", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task WebsiteLanPairingWithholdsSiteBytesAndUsesModeSpecificCookie()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(LanNetworkAddress.IsPrivateIpv4);
        if (address is null)
        {
            return;
        }

        var websitePath = Directory.CreateDirectory(System.IO.Path.Combine(_sandboxPath, "website-lan")).FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "index.html"), "private lesson");
        await File.WriteAllTextAsync(System.IO.Path.Combine(websitePath, "site.js"), "private script");
        using var security = LanSecurityMaterial.Create(address);
        using var pairing = new LanPairingSession();
        await using var server = new ServerController(new SharePathResolver());
        var port = ReserveAvailableLoopbackPort();
        await server.StartAsync(ServerOptions.WebsiteLanWithPairing(
            address,
            port,
            WebsiteDefinition.Create(websitePath),
            security.Certificate,
            pairing));
        var remoteAddress = Assert.Single(server.BaseAddresses, item => item.Scheme == Uri.UriSchemeHttps);
        using var handler = new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        using var client = new HttpClient(handler) { BaseAddress = remoteAddress };

        using var navigation = new HttpRequestMessage(HttpMethod.Get, "/");
        navigation.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        navigation.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        using var redirected = await client.SendAsync(navigation);
        Assert.Equal(HttpStatusCode.Redirect, redirected.StatusCode);

        using var subresource = new HttpRequestMessage(HttpMethod.Get, "/site.js");
        subresource.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
        subresource.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "script");
        using var withheld = await client.SendAsync(subresource);
        Assert.Equal(HttpStatusCode.Unauthorized, withheld.StatusCode);
        Assert.DoesNotContain("private script", await withheld.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var badOrigin = new HttpRequestMessage(HttpMethod.Post, "/_easyhttp/pair")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = pairing.Code }),
        };
        badOrigin.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
        using var rejected = await client.SendAsync(badOrigin);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        using var missingBrowserProof = new HttpRequestMessage(HttpMethod.Post, "/_easyhttp/pair")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = pairing.Code }),
        };
        using var missingBrowserProofResponse = await client.SendAsync(missingBrowserProof);
        Assert.Equal(HttpStatusCode.Forbidden, missingBrowserProofResponse.StatusCode);

        using var validPair = new HttpRequestMessage(HttpMethod.Post, "/_easyhttp/pair")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = pairing.Code }),
        };
        validPair.Headers.TryAddWithoutValidation("Origin", "null");
        validPair.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        validPair.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        validPair.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        using var paired = await client.SendAsync(validPair);
        Assert.Equal(HttpStatusCode.Redirect, paired.StatusCode);
        var cookie = Assert.Single(paired.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-EasyHttpSiteSession=", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("__Host-EasyHttpFilesSession=", cookie, StringComparison.Ordinal);

        using var allowed = await client.GetAsync("/");
        allowed.EnsureSuccessStatusCode();
        Assert.Equal("private lesson", await allowed.Content.ReadAsStringAsync());
    }

    private static async Task<byte[]> ReadUntilClosedAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            int count;
            try
            {
                count = await stream.ReadAsync(chunk, cancellationToken);
            }
            catch (IOException exception) when (exception.InnerException is SocketException
            {
                SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted,
            })
            {
                return buffer.ToArray();
            }

            if (count == 0)
            {
                return buffer.ToArray();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
    }

    private static int ReserveAvailableLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private int ServerPort => Assert.Single(_server?.BaseAddresses ?? []).Port;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Test server was not initialized.");
}
