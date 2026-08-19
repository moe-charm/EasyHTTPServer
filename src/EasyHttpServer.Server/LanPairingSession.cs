using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasyHttpServer.Server;

public enum PairingResult
{
    Succeeded,
    InvalidCode,
    Expired,
    Locked,
}

public sealed class LanPairingSession : IDisposable
{
    public const string FileSharingCookieName = "__Host-EasyHttpFilesSession";
    public const string WebsiteCookieName = "__Host-EasyHttpSiteSession";
    public const string CookieName = FileSharingCookieName;
    public const int MaximumFailedAttempts = 10;
    public const int MaximumSessions = 16;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly List<byte[]> _sessionTokenHashes = [];
    private string? _code;
    private DateTimeOffset _expiresAt;
    private int _failedAttempts;
    private bool _disposed;

    public LanPairingSession(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        IssueNewCode();
    }

    public string Code
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _code ?? throw new InvalidOperationException("No pairing code is active.");
            }
        }
    }

    public DateTimeOffset ExpiresAt
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _expiresAt;
            }
        }
    }

    public string IssueNewCode()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string nextCode;
            do
            {
                nextCode = RandomNumberGenerator.GetInt32(100_000_000).ToString("D8", CultureInfo.InvariantCulture);
            }
            while (nextCode == _code);

            _code = nextCode;
            _expiresAt = _timeProvider.GetUtcNow().Add(CodeLifetime);
            _failedAttempts = 0;
            return _code;
        }
    }

    public PairingResult TryPair(string suppliedCode, out string? sessionToken)
    {
        sessionToken = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_failedAttempts >= MaximumFailedAttempts)
            {
                return PairingResult.Locked;
            }

            if (_code is null || _timeProvider.GetUtcNow() >= _expiresAt)
            {
                _code = null;
                return PairingResult.Expired;
            }

            var supplied = Encoding.ASCII.GetBytes(suppliedCode);
            var expected = Encoding.ASCII.GetBytes(_code);
            if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
            {
                _failedAttempts++;
                if (_failedAttempts >= MaximumFailedAttempts)
                {
                    _code = null;
                    return PairingResult.Locked;
                }

                return PairingResult.InvalidCode;
            }

            sessionToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            if (_sessionTokenHashes.Count == MaximumSessions)
            {
                _sessionTokenHashes.RemoveAt(0);
            }

            _sessionTokenHashes.Add(HashToken(sessionToken));
            _code = null;
            return PairingResult.Succeeded;
        }
    }

    public bool IsAuthorized(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken) || sessionToken.Length > 128)
        {
            return false;
        }

        lock (_gate)
        {
            var suppliedHash = HashToken(sessionToken);
            return !_disposed && _sessionTokenHashes.Any(hash =>
                CryptographicOperations.FixedTimeEquals(hash, suppliedHash));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _code = null;
            _failedAttempts = 0;
            _sessionTokenHashes.Clear();
        }
    }

    private static byte[] HashToken(string token) =>
        SHA256.HashData(Encoding.ASCII.GetBytes(token));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
