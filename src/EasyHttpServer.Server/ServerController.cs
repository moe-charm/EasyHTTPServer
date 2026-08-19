using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using EasyHttpServer.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyHttpServer.Server;

public sealed class ServerController(ISharePathResolver pathResolver) : IServerController
{
    private static readonly string[] SupportedMethods = [HttpMethods.Get, HttpMethods.Head];
    private static readonly HashSet<string> DownloadOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".htm", ".html", ".xhtml", ".svg", ".svgz", ".js", ".mjs", ".xml", ".xsl", ".xslt",
    };
    private static readonly FileExtensionContentTypeProvider ContentTypes = CreateContentTypeProvider();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private WebApplication? _application;
    private PublishedRootLease? _websiteRootLease;
    private IReadOnlyList<Uri> _baseAddresses = [];
    private int _disposed;

    public bool IsRunning => _application is not null;

    public IReadOnlyList<Uri> BaseAddresses => _baseAddresses;

    public event EventHandler? StateChanged;

    public event EventHandler<TransferRecord>? TransferCompleted;

    public async Task StartAsync(ServerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ValidateOptions(options);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_application is not null)
            {
                throw new InvalidOperationException("The server is already running.");
            }

            var sharesBySlug = options.Shares.ToDictionary(share => share.Slug, StringComparer.Ordinal);
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(ServerController).Assembly.GetName().Name,
                Args = [],
            });

            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel => ConfigureKestrel(kestrel, options));
            builder.Services.AddRateLimiter(rateLimiter =>
            {
                rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress ?? IPAddress.None,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 240,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1),
                        }));
            });

            var application = builder.Build();
            PublishedRootLease? websiteRootLease = null;

            try
            {
                if (options.Website is { } website)
                {
                    websiteRootLease = new PublishedRootLease(website.RootPath);
                }

                ConfigurePipeline(application, sharesBySlug, pathResolver, options, websiteRootLease, record =>
                    TransferCompleted?.Invoke(this, record));
                await application.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await application.DisposeAsync().ConfigureAwait(false);
                websiteRootLease?.Dispose();
                throw;
            }

            var addresses = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()?
                .Addresses ?? [];

            _baseAddresses = addresses.Select(address => new Uri(address)).ToArray();
            _websiteRootLease = websiteRootLease;
            _application = application;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var stopped = false;

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var application = _application;
            if (application is null)
            {
                return;
            }

            try
            {
                await application.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await application.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    _websiteRootLease?.Dispose();
                    _websiteRootLease = null;
                    _application = null;
                    _baseAddresses = [];
                    stopped = true;
                }
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (stopped)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }

    private static void ValidateOptions(ServerOptions options)
    {
        if (options.Port is < 0 or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be between 0 and 65535.");
        }

        if (IPAddress.IsLoopback(options.ListenAddress))
        {
            if (options.LanSecurity is not null)
            {
                throw new InvalidOperationException("Loopback mode does not accept LAN credentials.");
            }
        }
        else
        {
            if (options.LanSecurity is null)
            {
                throw new InvalidOperationException("Remote network mode requires a TLS certificate and valid authentication.");
            }

            var addressAllowed = options.LanSecurity.NetworkKind switch
            {
                NetworkShareKind.Lan => LanNetworkAddress.IsPrivateIpv4(options.ListenAddress),
                NetworkShareKind.Vpn => VpnNetworkAddress.IsAllowedIpv4(options.ListenAddress),
                _ => false,
            };
            if (!addressAllowed)
            {
                throw new InvalidOperationException("The selected network mode does not allow this IPv4 listener.");
            }

            if (!IsValidRemoteCertificate(options.LanSecurity.Certificate, options.ListenAddress) ||
                !IsValidRemoteAuthentication(options.LanSecurity))
            {
                throw new InvalidOperationException("LAN mode requires a TLS certificate and valid authentication.");
            }
        }

        if (options.Publication is Publication.FileSharing && options.Shares.Count == 0)
        {
            throw new InvalidOperationException("At least one share is required.");
        }

        if (options.Shares.Any(share => !ShareSlug.IsValid(share.Slug)))
        {
            throw new InvalidOperationException("Every share must have a valid canonical slug.");
        }

        if (options.Shares.Select(share => share.Slug).Distinct(StringComparer.Ordinal).Count() != options.Shares.Count)
        {
            throw new InvalidOperationException("Share slugs must be unique.");
        }

        if (options.Website is { } website)
        {
            if (options.LanSecurity?.AccessCode is not null)
            {
                throw new InvalidOperationException("Website mode accepts pairing authentication only.");
            }

            if (!WebsitePathPolicy.IsLocalWebsiteRoot(website.RootPath))
            {
                throw new InvalidOperationException("Website root must be an existing local folder without reparse points.");
            }
        }
    }

    private static void ConfigureKestrel(KestrelServerOptions kestrel, ServerOptions options)
    {
        if (options.IncludesRemoteEndpoint)
        {
            kestrel.Listen(IPAddress.Loopback, options.Port);
        }

        kestrel.Listen(options.ListenAddress, options.Port, listen =>
        {
            if (options.LanSecurity is not null)
            {
                listen.UseHttps(options.LanSecurity.Certificate);
            }
        });
        kestrel.Limits.MaxConcurrentConnections = 100;
        kestrel.Limits.MaxConcurrentUpgradedConnections = 0;
        kestrel.Limits.MaxRequestBodySize = 1024;
        kestrel.Limits.MaxRequestBufferSize = 32 * 1024;
        kestrel.Limits.MaxRequestHeaderCount = 64;
        kestrel.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
        kestrel.Limits.MaxRequestLineSize = 8 * 1024;
        kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
        kestrel.Limits.MinRequestBodyDataRate = new MinDataRate(240, TimeSpan.FromSeconds(5));
        kestrel.Limits.MinResponseDataRate = new MinDataRate(240, TimeSpan.FromSeconds(10));
    }

    private static void ConfigurePipeline(
        WebApplication application,
        IReadOnlyDictionary<string, ShareDefinition> sharesBySlug,
        ISharePathResolver resolver,
        ServerOptions options,
        PublishedRootLease? websiteRootLease,
        Action<TransferRecord> transferCompleted)
    {
        application.UseRateLimiter();
        application.Use(async (context, next) =>
        {
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            context.Response.OnCompleted(() =>
            {
                stopwatch.Stop();
                transferCompleted(new TransferRecord(
                    startedAt,
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    context.Response.StatusCode,
                    context.Response.ContentLength,
                    stopwatch.Elapsed));
                return Task.CompletedTask;
            });

            ApplyAppGeneratedHeaders(context.Response);

            if (!IsAllowedHost(context, options))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var isRemoteRequest = IsRemoteRequest(context, options);
            var isPairingEndpoint = context.Request.Path.Equals("/_easyhttp/pair");
            if (isPairingEndpoint)
            {
                context.Response.Headers.CacheControl = "no-store";
            }
            if (isRemoteRequest && options.LanSecurity!.PairingSession is { } pairing)
            {
                var cookieName = options.IsWebsite
                    ? LanPairingSession.WebsiteCookieName
                    : LanPairingSession.FileSharingCookieName;
                if (!isPairingEndpoint && !pairing.IsAuthorized(context.Request.Cookies[cookieName]))
                {
                    if (HttpMethods.IsGet(context.Request.Method) &&
                        (!options.IsWebsite || IsTopLevelNavigation(context.Request)))
                    {
                        context.Response.Redirect("/_easyhttp/pair");
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return;
                }
            }
            else if (isRemoteRequest && options.LanSecurity!.AccessCode is { } accessCode &&
                     !HasValidAuthorization(context, accessCode))
            {
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"EasyHTTPServer 2\", charset=\"UTF-8\"";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if ((context.Request.ContentLength ?? 0) > 0 &&
                !(isRemoteRequest && isPairingEndpoint && HttpMethods.IsPost(context.Request.Method)))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var hasVerifiedPairingOrigin = isRemoteRequest &&
                                           isPairingEndpoint &&
                                           HttpMethods.IsPost(context.Request.Method) &&
                                           HasExpectedPairingOrigin(context, options);
            if (!IsAllowedFetchMetadata(context.Request) && !hasVerifiedPairingOrigin)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context).ConfigureAwait(false);
        });

        if (options.Publication is Publication.FileSharing)
        {
            application.MapMethods("/", SupportedMethods, () =>
                RenderShareIndex(sharesBySlug.Values));

            application.MapMethods("/s/{slug}", SupportedMethods, (HttpContext context) =>
                HandleShareRequest(context, sharesBySlug, resolver));

            application.MapMethods("/s/{slug}/{**relativePath}", SupportedMethods, (HttpContext context) =>
                HandleShareRequest(context, sharesBySlug, resolver));
        }
        else if (options.Website is { } website)
        {
            application.MapMethods("/", SupportedMethods, (HttpContext context) =>
                HandleWebsiteRequest(context, website, resolver, websiteRootLease!));
            application.MapMethods("/{**relativePath}", SupportedMethods, (HttpContext context) =>
                HandleWebsiteRequest(context, website, resolver, websiteRootLease!));
        }

        application.MapGet("/_easyhttp/pair", (HttpContext context) =>
            IsRemoteRequest(context, options) && options.LanSecurity?.PairingSession is not null
                ? RenderPairingPage()
                : Results.NotFound());

        application.MapPost("/_easyhttp/pair", async context =>
        {
            var result = await HandlePairingAsync(context, options).ConfigureAwait(false);
            await result.ExecuteAsync(context).ConfigureAwait(false);
        });
    }

    private static async Task<IResult> HandlePairingAsync(HttpContext context, ServerOptions options)
    {
        var pairing = options.LanSecurity?.PairingSession;
        if (!IsRemoteRequest(context, options) || pairing is null)
        {
            return Results.NotFound();
        }

        if (!context.Request.HasFormContentType || (context.Request.ContentLength ?? 0) > 1024)
        {
            return Results.BadRequest();
        }

        if (!HasExpectedPairingOrigin(context, options))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest();
        }

        var code = form["code"].ToString();
        if (code.Length != 8 || code.Any(character => character is < '0' or > '9'))
        {
            _ = pairing.TryPair(code, out _);
            return RenderPairingPage("コードが正しくありません。", StatusCodes.Status401Unauthorized);
        }

        var result = pairing.TryPair(code, out var token);
        if (result != PairingResult.Succeeded || token is null)
        {
            var status = result == PairingResult.Locked
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status401Unauthorized;
            return RenderPairingPage("コードが無効または期限切れです。PCで新しいコードを発行してください。", status);
        }

        var cookieName = options.IsWebsite
            ? LanPairingSession.WebsiteCookieName
            : LanPairingSession.FileSharingCookieName;
        context.Response.Cookies.Append(cookieName, token, new CookieOptions
        {
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
        return Results.Redirect("/");
    }

    private static IResult RenderPairingPage(string? message = null, int statusCode = StatusCodes.Status200OK)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>ペアリング</title>")
            .Append(Styles)
            .Append("</head><body><main><h1>EasyHTTPServer 2</h1><p>PCに表示された8桁のコードを入力してください。</p>");
        if (message is not null)
        {
            html.Append("<p>").Append(HtmlEncoder.Default.Encode(message)).Append("</p>");
        }

        html.Append("<form method=\"post\" action=\"/_easyhttp/pair\"><label>ペアリングコード <input name=\"code\" inputmode=\"numeric\" pattern=\"[0-9]{8}\" maxlength=\"8\" required></label> <button type=\"submit\">接続</button></form></main></body></html>");
        return Results.Text(
            html.ToString(),
            contentType: "text/html; charset=utf-8",
            contentEncoding: Encoding.UTF8,
            statusCode: statusCode);
    }

    private static IResult HandleShareRequest(
        HttpContext context,
        IReadOnlyDictionary<string, ShareDefinition> sharesBySlug,
        ISharePathResolver resolver)
    {
        var slug = context.Request.RouteValues["slug"]?.ToString();
        if (slug is null || !sharesBySlug.TryGetValue(slug, out var share))
        {
            return Results.NotFound();
        }

        var routedPath = context.Request.RouteValues["relativePath"]?.ToString() ?? string.Empty;
        var relativePath = routedPath.EndsWith('/')
            ? routedPath[..^1]
            : routedPath;
        var resolution = resolver.Resolve(share, relativePath);
        if (!resolution.IsSuccess || resolution.FullPath is null)
        {
            return resolution.Status switch
            {
                PathResolutionStatus.InvalidPath or PathResolutionStatus.OutsideRoot or PathResolutionStatus.ReparsePoint =>
                    Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.NotFound(),
            };
        }

        if (resolution.IsDirectory)
        {
            if (share.PreferIndexFile)
            {
                var indexRelativePath = string.IsNullOrEmpty(relativePath)
                    ? "index.html"
                    : $"{relativePath}/index.html";
                var index = resolver.Resolve(share, indexRelativePath);
                if (index.IsSuccess && !index.IsDirectory && index.FullPath is not null)
                {
                    return CreateFileResult(context, index.FullPath, website: false);
                }
            }

            return share.DirectoryBrowsingEnabled
                ? RenderDirectoryListing(share, resolution.FullPath, relativePath)
                : Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return CreateFileResult(context, resolution.FullPath, website: false);
    }

    private static IResult HandleWebsiteRequest(
        HttpContext context,
        WebsiteDefinition website,
        ISharePathResolver resolver,
        PublishedRootLease rootLease)
    {
        var relativePath = (context.Request.RouteValues["relativePath"]?.ToString() ?? string.Empty).TrimEnd('/');
        if (!WebsitePathPolicy.IsAllowed(relativePath))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var root = new ShareDefinition(
            Guid.Empty,
            "Website",
            "website",
            website.RootPath,
            DirectoryBrowsingEnabled: false,
            PreferIndexFile: false);
        var resolution = resolver.Resolve(root, relativePath);
        if (!resolution.IsSuccess || resolution.FullPath is null)
        {
            return resolution.Status switch
            {
                PathResolutionStatus.InvalidPath or PathResolutionStatus.OutsideRoot or PathResolutionStatus.ReparsePoint =>
                    Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.NotFound(),
            };
        }

        if (resolution.IsDirectory)
        {
            if (!context.Request.Path.Value!.EndsWith('/'))
            {
                ApplyWebsiteHeaders(context.Response);
                return Results.Redirect($"{context.Request.Path}/", permanent: true, preserveMethod: true);
            }

            foreach (var indexName in new[] { "index.html", "index.htm" })
            {
                var indexRelativePath = string.IsNullOrEmpty(relativePath)
                    ? indexName
                    : $"{relativePath}/{indexName}";
                var index = resolver.Resolve(root, indexRelativePath);
                if (index.IsSuccess && !index.IsDirectory && index.FullPath is not null &&
                    WebsitePathPolicy.IsAllowedFile(index.FullPath))
                {
                    return CreateFileResult(context, index.FullPath, website: true, rootLease);
                }
            }

            return Results.NotFound();
        }

        return WebsitePathPolicy.IsAllowedFile(resolution.FullPath)
            ? CreateFileResult(context, resolution.FullPath, website: true, rootLease)
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult CreateFileResult(
        HttpContext context,
        string fullPath,
        bool website,
        PublishedRootLease? rootLease = null)
    {
        var knownType = ContentTypes.TryGetContentType(fullPath, out var contentType);
        var downloadOnly = !knownType || !website && DownloadOnlyExtensions.Contains(Path.GetExtension(fullPath));
        if (website)
        {
            ApplyWebsiteHeaders(context.Response);
        }
        else
        {
            ApplyFileShareHeaders(context.Response);
        }

        var mediaType = contentType ?? "application/octet-stream";
        var downloadName = downloadOnly ? Path.GetFileName(fullPath) : null;
        if (!website)
        {
            return Results.File(
                fullPath,
                mediaType,
                fileDownloadName: downloadName,
                enableRangeProcessing: true);
        }

        try
        {
            return Results.File(
                rootLease!.OpenFile(fullPath),
                mediaType,
                fileDownloadName: downloadName,
                enableRangeProcessing: true);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (IOException)
        {
            return Results.NotFound();
        }
    }

    private static IResult RenderShareIndex(IEnumerable<ShareDefinition> shares)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"><title>EasyHTTPServer 2</title>")
            .Append(Styles)
            .Append("</head><body><main><h1>EasyHTTPServer 2</h1><p>公開中の共有</p><ul>");

        foreach (var share in shares.OrderBy(share => share.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            html.Append("<li><a href=\"/s/")
                .Append(Uri.EscapeDataString(share.Slug))
                .Append("/\">")
                .Append(HtmlEncoder.Default.Encode(share.Name))
                .Append("</a></li>");
        }

        html.Append("</ul></main></body></html>");
        return Results.Content(html.ToString(), "text/html; charset=utf-8", Encoding.UTF8);
    }

    private static IResult RenderDirectoryListing(
        ShareDefinition share,
        string directoryPath,
        string relativePath)
    {
        var displayPath = string.IsNullOrEmpty(relativePath) ? "/" : $"/{relativePath}/";
        var html = new StringBuilder("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"><title>")
            .Append(HtmlEncoder.Default.Encode(share.Name))
            .Append("</title>")
            .Append(Styles)
            .Append("</head><body><main><h1>")
            .Append(HtmlEncoder.Default.Encode(share.Name))
            .Append("</h1><p class=\"path\">")
            .Append(HtmlEncoder.Default.Encode(displayPath))
            .Append("</p><ul>");

        if (!string.IsNullOrEmpty(relativePath))
        {
            var parent = relativePath.Contains('/') ? relativePath[..relativePath.LastIndexOf('/')] : string.Empty;
            html.Append("<li><a href=\"/s/")
                .Append(Uri.EscapeDataString(share.Slug))
                .Append('/')
                .Append(EncodeRelativePath(parent))
                .Append("\">../</a></li>");
        }

        try
        {
            foreach (var entry in new DirectoryInfo(directoryPath)
                         .EnumerateFileSystemInfos()
                         .Where(entry => (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                         .OrderByDescending(entry => entry is DirectoryInfo)
                         .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var isDirectory = entry is DirectoryInfo;
                var childRelativePath = string.IsNullOrEmpty(relativePath)
                    ? entry.Name
                    : $"{relativePath}/{entry.Name}";

                html.Append("<li><a href=\"/s/")
                    .Append(Uri.EscapeDataString(share.Slug))
                    .Append('/')
                    .Append(EncodeRelativePath(childRelativePath))
                    .Append(isDirectory ? "/\">" : "\">")
                    .Append(HtmlEncoder.Default.Encode(entry.Name))
                    .Append(isDirectory ? "/" : string.Empty)
                    .Append("</a></li>");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        html.Append("</ul></main></body></html>");
        return Results.Content(html.ToString(), "text/html; charset=utf-8", Encoding.UTF8);
    }

    private static string EncodeRelativePath(string relativePath) =>
        string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));

    private static bool IsAllowedHost(HttpContext context, ServerOptions options)
    {
        var localAddress = context.Connection.LocalIpAddress;
        var host = context.Request.Host.Host;
        if (localAddress is not null && IPAddress.IsLoopback(localAddress))
        {
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   IPAddress.TryParse(host, out var loopback) && IPAddress.IsLoopback(loopback);
        }

        return IsRemoteRequest(context, options) &&
               IPAddress.TryParse(host, out var address) && address.Equals(options.ListenAddress) &&
               (context.Request.Host.Port ?? 443) == (options.Port == 0 ? context.Connection.LocalPort : options.Port);
    }

    private static bool IsRemoteRequest(HttpContext context, ServerOptions options) =>
        options.LanSecurity is not null &&
        context.Connection.LocalIpAddress is { } localAddress &&
        localAddress.Equals(options.ListenAddress);

    private static bool HasValidAuthorization(HttpContext context, string expectedAccessCode)
    {
        var header = context.Request.Headers.Authorization;
        if (header.Count != 1 || !header.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var encoded = header.ToString()["Basic ".Length..].Trim();
            if (encoded.Length > 256)
            {
                return false;
            }

            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var separator = credentials.IndexOf(':');
            if (separator < 0 || !credentials[..separator].Equals("easyhttp", StringComparison.Ordinal))
            {
                return false;
            }

            var supplied = Encoding.ASCII.GetBytes(credentials[(separator + 1)..]);
            var expected = Encoding.ASCII.GetBytes(expectedAccessCode);
            return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidAccessCode(string value) =>
        value.Length == 32 && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool IsValidRemoteAuthentication(LanSecurityOptions security) =>
        security switch
        {
            { AccessCode: { } accessCode, PairingSession: null } => IsValidAccessCode(accessCode),
            { AccessCode: null, PairingSession: not null } => true,
            _ => false,
        };

    private static bool HasExpectedPairingOrigin(HttpContext context, ServerOptions options)
    {
        if (!IsRemoteRequest(context, options))
        {
            return false;
        }

        var port = options.Port == 0 ? context.Connection.LocalPort : options.Port;
        var expected = new UriBuilder(Uri.UriSchemeHttps, options.ListenAddress.ToString(), port).Uri
            .GetLeftPart(UriPartial.Authority);
        var origin = context.Request.Headers.Origin;
        if (origin.Count == 1 && string.Equals(origin.ToString(), expected, StringComparison.Ordinal))
        {
            return true;
        }

        if (origin.Count > 1 ||
            (origin.Count == 1 && !origin.ToString().Equals("null", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return HasSameOriginFormNavigationMetadata(context.Request);
    }

    private static bool HasSameOriginFormNavigationMetadata(HttpRequest request) =>
        request.Headers["Sec-Fetch-Site"].ToString().Equals("same-origin", StringComparison.OrdinalIgnoreCase) &&
        request.Headers["Sec-Fetch-Mode"].ToString().Equals("navigate", StringComparison.OrdinalIgnoreCase) &&
        request.Headers["Sec-Fetch-Dest"].ToString().Equals("document", StringComparison.OrdinalIgnoreCase);

    private static bool IsTopLevelNavigation(HttpRequest request)
    {
        var hasMode = request.Headers.TryGetValue("Sec-Fetch-Mode", out var mode);
        var hasDestination = request.Headers.TryGetValue("Sec-Fetch-Dest", out var destination);
        if (!hasMode && !hasDestination)
        {
            return true;
        }

        return mode.ToString().Equals("navigate", StringComparison.OrdinalIgnoreCase) &&
               destination.ToString().Equals("document", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedFetchMetadata(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Sec-Fetch-Site", out var site))
        {
            return true;
        }

        var siteValue = site.ToString();
        if (siteValue.Equals("same-origin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!siteValue.Equals("none", StringComparison.OrdinalIgnoreCase) &&
            !siteValue.Equals("same-site", StringComparison.OrdinalIgnoreCase) &&
            !siteValue.Equals("cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isNavigation = HttpMethods.IsGet(request.Method) && IsTopLevelNavigation(request);
        if (!isNavigation)
        {
            return false;
        }

        return siteValue.Equals("none", StringComparison.OrdinalIgnoreCase) ||
               request.Headers["Sec-Fetch-User"].ToString().Equals("?1", StringComparison.Ordinal);
    }

    private static void ApplyAppGeneratedHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.XFrameOptions = "DENY";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers.CacheControl = "no-store";
    }

    private static void ApplyFileShareHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = "default-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors 'none'";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.XFrameOptions = "DENY";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers.CacheControl = "private, no-store";
    }

    private static void ApplyWebsiteHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self'; frame-src 'self'; form-action 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; media-src 'self' blob:; connect-src 'self'; manifest-src 'self'; worker-src 'none'";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.XFrameOptions = "SAMEORIGIN";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers.CacheControl = "private, no-store";
    }

    private static bool IsValidRemoteCertificate(X509Certificate2 certificate, IPAddress address)
    {
        var now = DateTime.UtcNow;
        if (!certificate.HasPrivateKey || now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            return false;
        }

        var constraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().SingleOrDefault();
        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        var enhancedUsage = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        var san = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().SingleOrDefault();
        return constraints is { CertificateAuthority: false } &&
               keyUsage is not null && keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature) &&
               enhancedUsage is not null && enhancedUsage.EnhancedKeyUsages.Cast<Oid>()
                   .Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1") &&
               san is not null && san.EnumerateIPAddresses().Contains(address);
    }

    private static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".mjs"] = "text/javascript";
        provider.Mappings[".wasm"] = "application/wasm";
        provider.Mappings[".webmanifest"] = "application/manifest+json";
        provider.Mappings[".avif"] = "image/avif";
        provider.Mappings[".woff"] = "font/woff";
        provider.Mappings[".woff2"] = "font/woff2";
        provider.Mappings[".ttf"] = "font/ttf";
        provider.Mappings[".otf"] = "font/otf";
        return provider;
    }

    private const string Styles = "<style>body{font-family:system-ui,sans-serif;background:#f5f7fb;color:#18212f;margin:0}main{max-width:60rem;margin:3rem auto;background:white;padding:2rem;border-radius:1rem;box-shadow:0 10px 30px #10204018}a{color:#075ecb}.path{color:#52606f}li{margin:.5rem 0}</style>";
}
