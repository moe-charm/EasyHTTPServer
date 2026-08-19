using System.Net;
using EasyHttpServer.Core;

namespace EasyHttpServer.Server.Tests;

public sealed class ServerControllerTests
{
    [Fact]
    public async Task DisposeAsyncCanBeCalledMoreThanOnce()
    {
        var controller = new ServerController(new SharePathResolver());

        await controller.DisposeAsync();
        await controller.DisposeAsync();
    }

    [Fact]
    public async Task StartAsyncRejectsNonLoopbackListener()
    {
        using var sandbox = new TemporaryDirectory();
        var share = ShareDefinition.Create("Test", "test", sandbox.Path);
        await using var controller = new ServerController(new SharePathResolver());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.StartAsync(new ServerOptions(IPAddress.Any, 8080, [share])));

        Assert.Contains("TLS certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsyncRejectsPublicAddressEvenWithAuthenticationMaterial()
    {
        using var sandbox = new TemporaryDirectory();
        var certificateAddress = IPAddress.Parse("192.168.1.20");
        var share = ShareDefinition.Create("Test", "test", sandbox.Path);
        using var security = LanSecurityMaterial.Create(certificateAddress);
        using var pairing = new LanPairingSession();
        await using var controller = new ServerController(new SharePathResolver());
        var options = new ServerOptions(
            IPAddress.Parse("203.0.113.10"),
            8080,
            [share],
            LanSecurityOptions.Pairing(security.Certificate, pairing, NetworkShareKind.Vpn));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartAsync(options));

        Assert.Contains("does not allow", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsyncRejectsPrivateListenerWithoutTlsAndAccessCode()
    {
        using var sandbox = new TemporaryDirectory();
        var share = ShareDefinition.Create("Test", "test", sandbox.Path);
        await using var controller = new ServerController(new SharePathResolver());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.StartAsync(new ServerOptions(IPAddress.Parse("192.168.1.20"), 8080, [share])));

        Assert.Contains("TLS certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsyncRejectsWeakLanAccessCode()
    {
        using var sandbox = new TemporaryDirectory();
        var address = IPAddress.Parse("192.168.1.20");
        var share = ShareDefinition.Create("Test", "test", sandbox.Path);
        using var security = LanSecurityMaterial.Create(address);
        await using var controller = new ServerController(new SharePathResolver());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartAsync(
            ServerOptions.Lan(address, 8080, [share], security.Certificate, "password")));

        Assert.Contains("valid authentication", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebsiteModeRejectsBasicAuthenticationConfiguration()
    {
        using var sandbox = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(sandbox.Path, "index.html"), "site");
        var address = IPAddress.Parse("192.168.1.20");
        using var security = LanSecurityMaterial.Create(address);
        await using var controller = new ServerController(new SharePathResolver());
        var options = new ServerOptions(
            address,
            8080,
            new Publication.Website(WebsiteDefinition.Create(sandbox.Path)),
            LanSecurityOptions.Basic(
                security.Certificate,
                security.AccessCode,
                NetworkShareKind.Lan));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartAsync(options));

        Assert.Contains("pairing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
