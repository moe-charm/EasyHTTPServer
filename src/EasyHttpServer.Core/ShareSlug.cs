using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyHttpServer.Core;

public static partial class ShareSlug
{
    public const int MaximumLength = 64;

    public static bool IsValid(string? value) =>
        value is not null && SlugPattern().IsMatch(value);

    public static string FromDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var slug = InvalidCharacters().Replace(displayName.Trim().ToLowerInvariant(), "-").Trim('-');
        slug = RepeatedHyphens().Replace(slug, "-");

        if (slug.Length > MaximumLength)
        {
            slug = slug[..MaximumLength].TrimEnd('-');
        }

        if (IsValid(slug))
        {
            return slug;
        }

        var normalizedName = displayName.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName));
        return $"share-{Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant()}";
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidCharacters();

    [GeneratedRegex("-+", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedHyphens();
}
