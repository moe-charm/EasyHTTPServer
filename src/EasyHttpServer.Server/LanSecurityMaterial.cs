using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EasyHttpServer.Server;

public sealed class LanSecurityMaterial : IDisposable
{
    private LanSecurityMaterial(X509Certificate2 certificate, string accessCode)
    {
        Certificate = certificate;
        AccessCode = accessCode;
        Fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
    }

    public X509Certificate2 Certificate { get; }

    public string AccessCode { get; }

    public string Fingerprint { get; }

    public static LanSecurityMaterial Create(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!LanNetworkAddress.IsPrivateIpv4(address) && !VpnNetworkAddress.IsCarrierGradeNatIpv4(address))
        {
            throw new ArgumentException("A private or CGNAT IPv4 address is required.", nameof(address));
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            $"CN={address}",
            key,
            HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(CreateServerAuthenticationExtension());
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(address);
        request.CertificateExtensions.Add(san.Build(critical: true));

        var now = DateTimeOffset.UtcNow;
        using var generated = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(7));
        var certificate = X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pkcs12),
            password: null,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        var accessCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return new LanSecurityMaterial(certificate, accessCode);
    }

    public void Dispose() => Certificate.Dispose();

    private static X509EnhancedKeyUsageExtension CreateServerAuthenticationExtension()
    {
        var usages = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
        return new X509EnhancedKeyUsageExtension(usages, true);
    }
}

public static class VpnNetworkAddress
{
    public static bool IsAllowedIpv4(IPAddress address) =>
        LanNetworkAddress.IsPrivateIpv4(address) || IsCarrierGradeNatIpv4(address);

    public static bool IsCarrierGradeNatIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }
}

public static class LanNetworkAddress
{
    public static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               (bytes[0] == 10 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                bytes[0] == 192 && bytes[1] == 168);
    }
}
