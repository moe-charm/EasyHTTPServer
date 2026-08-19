using System.Net;
using System.Security.Cryptography.X509Certificates;
using EasyHttpServer.Core;

namespace EasyHttpServer.Server;

public sealed record ServerOptions(
    IPAddress ListenAddress,
    int Port,
    Publication Publication,
    LanSecurityOptions? LanSecurity = null)
{
    public bool IncludesRemoteEndpoint => LanSecurity is not null;

    public IReadOnlyList<ShareDefinition> Shares =>
        Publication is Publication.FileSharing fileSharing ? fileSharing.Shares : [];

    public WebsiteDefinition? Website =>
        Publication is Publication.Website website ? website.Definition : null;

    public bool IsWebsite => Publication is Publication.Website;

    public ServerOptions(
        IPAddress listenAddress,
        int port,
        IReadOnlyList<ShareDefinition> shares,
        LanSecurityOptions? lanSecurity = null)
        : this(listenAddress, port, new Publication.FileSharing(shares), lanSecurity)
    {
    }

    public static ServerOptions Loopback(int port, IReadOnlyList<ShareDefinition> shares) =>
        new(IPAddress.Loopback, port, shares);

    public static ServerOptions WebsiteLoopback(int port, WebsiteDefinition website) =>
        new(IPAddress.Loopback, port, new Publication.Website(website));

    public static ServerOptions Lan(
        IPAddress address,
        int port,
        IReadOnlyList<ShareDefinition> shares,
        X509Certificate2 certificate,
        string accessCode) =>
        new(address, port, shares, LanSecurityOptions.Basic(certificate, accessCode, NetworkShareKind.Lan));

    public static ServerOptions LanWithPairing(
        IPAddress address,
        int port,
        IReadOnlyList<ShareDefinition> shares,
        X509Certificate2 certificate,
        LanPairingSession pairing) =>
        new(address, port, shares, LanSecurityOptions.Pairing(certificate, pairing, NetworkShareKind.Lan));

    public static ServerOptions VpnWithPairing(
        IPAddress address,
        int port,
        IReadOnlyList<ShareDefinition> shares,
        X509Certificate2 certificate,
        LanPairingSession pairing) =>
        new(address, port, shares, LanSecurityOptions.Pairing(certificate, pairing, NetworkShareKind.Vpn));

    public static ServerOptions WebsiteLanWithPairing(
        IPAddress address,
        int port,
        WebsiteDefinition website,
        X509Certificate2 certificate,
        LanPairingSession pairing) =>
        new(address, port, new Publication.Website(website),
            LanSecurityOptions.Pairing(certificate, pairing, NetworkShareKind.Lan));

    public static ServerOptions WebsiteVpnWithPairing(
        IPAddress address,
        int port,
        WebsiteDefinition website,
        X509Certificate2 certificate,
        LanPairingSession pairing) =>
        new(address, port, new Publication.Website(website),
            LanSecurityOptions.Pairing(certificate, pairing, NetworkShareKind.Vpn));
}

public enum NetworkShareKind
{
    Lan,
    Vpn,
}

public sealed record LanSecurityOptions(
    X509Certificate2 Certificate,
    string? AccessCode,
    LanPairingSession? PairingSession,
    NetworkShareKind NetworkKind)
{
    public static LanSecurityOptions Basic(
        X509Certificate2 certificate,
        string accessCode,
        NetworkShareKind networkKind) =>
        new(certificate, accessCode, null, networkKind);

    public static LanSecurityOptions Pairing(
        X509Certificate2 certificate,
        LanPairingSession pairingSession,
        NetworkShareKind networkKind) =>
        new(certificate, null, pairingSession, networkKind);
}
