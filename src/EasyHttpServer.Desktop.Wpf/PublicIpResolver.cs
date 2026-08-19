using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.IO;

namespace EasyHttpServer.Desktop.Wpf;

public interface IPublicIpResolver
{
    Task<IPAddress> ResolveIpv4Async(CancellationToken cancellationToken = default);
}

public sealed class AwsPublicIpResolver : IPublicIpResolver, IDisposable
{
    internal static readonly Uri Endpoint = new("https://checkip.amazonaws.com/");
    private const int MaximumResponseBytes = 128;
    private readonly HttpClient _client;
    private readonly TimeSpan _requestTimeout;

    public AwsPublicIpResolver()
        : this(new SocketsHttpHandler { AllowAutoRedirect = false }, TimeSpan.FromSeconds(5))
    {
    }

    internal AwsPublicIpResolver(HttpMessageHandler handler, TimeSpan requestTimeout)
    {
        _client = new HttpClient(handler, disposeHandler: true);
        _requestTimeout = requestTimeout;
    }

    public async Task<IPAddress> ResolveIpv4Async(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new InvalidDataException("グローバルIP確認サービスの応答が大きすぎます");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var buffer = new byte[MaximumResponseBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length > MaximumResponseBytes)
        {
            throw new InvalidDataException("グローバルIP確認サービスの応答が大きすぎます");
        }

        var value = System.Text.Encoding.ASCII.GetString(buffer, 0, length).Trim();
        if (!IPAddress.TryParse(value, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new InvalidDataException("グローバルIP確認サービスから有効なIPv4を取得できませんでした");
        }

        return address;
    }

    public void Dispose() => _client.Dispose();
}

internal sealed class UnavailablePublicIpResolver : IPublicIpResolver
{
    public Task<IPAddress> ResolveIpv4Async(CancellationToken cancellationToken = default) =>
        Task.FromException<IPAddress>(new InvalidOperationException("グローバルIP確認機能を初期化できませんでした"));
}
