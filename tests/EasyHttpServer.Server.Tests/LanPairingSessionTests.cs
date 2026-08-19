using EasyHttpServer.Server;

namespace EasyHttpServer.Server.Tests;

public sealed class LanPairingSessionTests
{
    [Fact]
    public void CodeIsEightDigitsAndFirstSuccessInvalidatesIt()
    {
        using var pairing = new LanPairingSession();
        var code = pairing.Code;

        Assert.Matches("^[0-9]{8}$", code);
        Assert.Equal(PairingResult.Succeeded, pairing.TryPair(code, out var token));
        Assert.NotNull(token);
        Assert.True(pairing.IsAuthorized(token));
        Assert.Equal(PairingResult.Expired, pairing.TryPair(code, out _));
    }

    [Fact]
    public void TenFailuresLockCodeUntilRenewed()
    {
        using var pairing = new LanPairingSession();

        for (var attempt = 1; attempt < LanPairingSession.MaximumFailedAttempts; attempt++)
        {
            Assert.Equal(PairingResult.InvalidCode, pairing.TryPair("999999999", out _));
        }

        Assert.Equal(PairingResult.Locked, pairing.TryPair("999999999", out _));
        var renewed = pairing.IssueNewCode();
        Assert.Equal(PairingResult.Succeeded, pairing.TryPair(renewed, out _));
    }

    [Fact]
    public void CodeExpiresAfterFiveMinutes()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var pairing = new LanPairingSession(clock);
        var code = pairing.Code;

        clock.Advance(LanPairingSession.CodeLifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(PairingResult.Expired, pairing.TryPair(code, out _));
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }
}
