using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EasyHttpServer.Server;

namespace EasyHttpServer.Server.Tests;

public sealed class LanSecurityMaterialTests
{
    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("8.8.8.8", false)]
    public void PrivateIpv4ClassificationIsStrict(string value, bool expected) =>
        Assert.Equal(expected, LanNetworkAddress.IsPrivateIpv4(IPAddress.Parse(value)));

    [Theory]
    [InlineData("100.64.0.0", true)]
    [InlineData("100.98.85.123", true)]
    [InlineData("100.127.255.255", true)]
    [InlineData("100.63.255.255", false)]
    [InlineData("100.128.0.0", false)]
    [InlineData("8.8.8.8", false)]
    public void CarrierGradeNatClassificationIsStrict(string value, bool expected) =>
        Assert.Equal(expected, VpnNetworkAddress.IsCarrierGradeNatIpv4(IPAddress.Parse(value)));

    [Fact]
    public void VpnAllowsPrivateAndCarrierGradeNatButNotPublicAddresses()
    {
        Assert.True(VpnNetworkAddress.IsAllowedIpv4(IPAddress.Parse("10.10.10.10")));
        Assert.True(VpnNetworkAddress.IsAllowedIpv4(IPAddress.Parse("100.100.100.100")));
        Assert.False(VpnNetworkAddress.IsAllowedIpv4(IPAddress.Parse("203.0.113.10")));
    }

    [Fact]
    public void GeneratedMaterialHasRequiredCertificateProfileAndSecretStrength()
    {
        var address = IPAddress.Parse("192.168.1.20");
        using var material = LanSecurityMaterial.Create(address);

        Assert.True(material.Certificate.HasPrivateKey);
        Assert.InRange(material.Certificate.NotAfter - material.Certificate.NotBefore,
            TimeSpan.FromDays(7), TimeSpan.FromDays(7.01));
        var constraints = Assert.Single(material.Certificate.Extensions.OfType<X509BasicConstraintsExtension>());
        Assert.False(constraints.CertificateAuthority);
        var keyUsage = Assert.Single(material.Certificate.Extensions.OfType<X509KeyUsageExtension>());
        Assert.Equal(X509KeyUsageFlags.DigitalSignature, keyUsage.KeyUsages);
        var enhancedUsage = Assert.Single(material.Certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>());
        Assert.Contains(enhancedUsage.EnhancedKeyUsages.Cast<Oid>(), oid => oid.Value == "1.3.6.1.5.5.7.3.1");
        var san = Assert.Single(material.Certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>());
        Assert.Equal(address, Assert.Single(san.EnumerateIPAddresses()));
        Assert.Matches("^[0-9A-F]{32}$", material.AccessCode);
        Assert.Matches("^[0-9A-F]{64}$", material.Fingerprint);
    }

    [Fact]
    public void EachSessionReceivesDifferentAccessCodeAndCertificate()
    {
        var address = IPAddress.Parse("10.0.0.2");
        using var first = LanSecurityMaterial.Create(address);
        using var second = LanSecurityMaterial.Create(address);

        Assert.NotEqual(first.AccessCode, second.AccessCode);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }
}
