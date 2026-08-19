using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using EasyHttpServer.Desktop.Wpf;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class PublicIpResolverTests
{
    [Fact]
    public async Task ResolvesIpv4FromAwsHttpsEndpoint()
    {
        Uri? requestedUri = null;
        using var resolver = CreateResolver(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("203.0.113.42\n", Encoding.ASCII),
            };
        });

        var result = await resolver.ResolveIpv4Async();

        Assert.Equal(IPAddress.Parse("203.0.113.42"), result);
        Assert.Equal("https", requestedUri?.Scheme);
        Assert.Equal("checkip.amazonaws.com", requestedUri?.Host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("2001:db8::1")]
    [InlineData("203.0.113.1 extra")]
    public async Task RejectsAnythingExceptOneIpv4(string body)
    {
        using var resolver = CreateResolver(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.ASCII),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => resolver.ResolveIpv4Async());
    }

    [Fact]
    public async Task RejectsOversizedResponse()
    {
        using var resolver = CreateResolver(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('1', 129), Encoding.ASCII),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => resolver.ResolveIpv4Async());
    }

    [Fact]
    public async Task RejectsRedirectInsteadOfFollowingIt()
    {
        using var resolver = CreateResolver(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("http://example.invalid/") },
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => resolver.ResolveIpv4Async());
    }

    [Fact]
    public async Task CancelsSlowResponseAtConfiguredTimeout()
    {
        using var resolver = new AwsPublicIpResolver(
            new DelegateHandler(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveIpv4Async());
    }

    private static AwsPublicIpResolver CreateResolver(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new DelegateHandler((request, _) => Task.FromResult(response(request))), TimeSpan.FromSeconds(1));

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
