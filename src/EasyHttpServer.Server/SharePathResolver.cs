using EasyHttpServer.Core;

namespace EasyHttpServer.Server;

public sealed class SharePathResolver : ISharePathResolver
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public PathResolution Resolve(ShareDefinition share, string? untrustedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(share);

        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(share.RootPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(PathResolutionStatus.InvalidPath);
        }

        if (!Directory.Exists(root))
        {
            return new(PathResolutionStatus.NotFound);
        }

        var relativePath = untrustedRelativePath ?? string.Empty;
        if (!TryValidateSegments(relativePath, out var segments))
        {
            return new(PathResolutionStatus.InvalidPath);
        }

        string candidate;
        try
        {
            candidate = segments.Length == 0
                ? root
                : Path.GetFullPath(Path.Combine([root, .. segments]));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(PathResolutionStatus.InvalidPath);
        }

        var relativeToRoot = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relativeToRoot) ||
            relativeToRoot.Equals("..", StringComparison.Ordinal) ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativeToRoot.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return new(PathResolutionStatus.OutsideRoot);
        }

        if (ContainsReparsePoint(root, segments))
        {
            return new(PathResolutionStatus.ReparsePoint);
        }

        if (Directory.Exists(candidate))
        {
            return new(PathResolutionStatus.Success, candidate, IsDirectory: true);
        }

        if (File.Exists(candidate))
        {
            return new(PathResolutionStatus.Success, candidate, IsDirectory: false);
        }

        return new(PathResolutionStatus.NotFound);
    }

    private static bool TryValidateSegments(string relativePath, out string[] segments)
    {
        segments = [];

        if (relativePath.Contains('\0') || relativePath.Contains('\\') || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        if (relativePath.Length == 0)
        {
            return true;
        }

        var rawSegments = relativePath.Split('/', StringSplitOptions.None);
        if (rawSegments.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var validated = new string[rawSegments.Length];
        for (var index = 0; index < rawSegments.Length; index++)
        {
            var segment = rawSegments[index];
            if (!TryDecodeSegment(segment, out var decoded) || !IsValidSegment(decoded))
            {
                return false;
            }

            // ASP.NET Core has already decoded the route value once. Decode again only
            // to validate nested encodings; resolve the literal route segment so a
            // legitimate file such as "a%20b.txt" does not become "a b.txt".
            validated[index] = segment;
        }

        segments = validated;
        return true;
    }

    private static bool TryDecodeSegment(string segment, out string decoded)
    {
        decoded = segment;
        try
        {
            for (var pass = 0; pass < 2; pass++)
            {
                var next = Uri.UnescapeDataString(decoded);
                if (next == decoded)
                {
                    break;
                }

                decoded = next;
            }
        }
        catch (UriFormatException)
        {
            return false;
        }

        return true;
    }

    private static bool IsValidSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) ||
            segment is "." or ".." ||
            segment.Contains('/') ||
            segment.Contains('\\') ||
            segment.Contains(':') ||
            segment.Contains('\0') ||
            segment.EndsWith('.') ||
            segment.EndsWith(' '))
        {
            return false;
        }

        var deviceStem = segment.Split('.', 2)[0].TrimEnd(' ', '.');
        return !ReservedDeviceNames.Contains(deviceStem);
    }

    private static bool ContainsReparsePoint(string root, IReadOnlyList<string> segments)
    {
        if (IsReparsePoint(root))
        {
            return true;
        }

        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                break;
            }

            if (IsReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
