using System.Text.RegularExpressions;

namespace EasyHttpServer.Server;

internal static partial class WebsitePathPolicy
{
    private static readonly HashSet<string> DeniedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cgi", ".pl", ".php", ".phtml", ".asp", ".aspx", ".cshtml", ".razor",
        ".pfx", ".p12", ".key", ".pem",
    };

    public static bool IsAllowed(string relativePath)
    {
        if (relativePath.Length == 0)
        {
            return true;
        }

        var segments = relativePath.Split('/', StringSplitOptions.None);
        if (segments.Any(segment => segment.StartsWith('.') ||
                                    segment.Equals("_easyhttp", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return IsAllowedName(segments[^1]);
    }

    public static bool IsLocalWebsiteRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) ||
            rootPath.StartsWith("\\\\", StringComparison.Ordinal) ||
            rootPath.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            rootPath.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot) || new DriveInfo(pathRoot).DriveType == DriveType.Network ||
                !Directory.Exists(fullPath))
            {
                return false;
            }

            var current = new DirectoryInfo(fullPath);
            while (current is not null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                current = current.Parent;
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    public static bool IsAllowedFile(string fullPath)
    {
        try
        {
            var attributes = File.GetAttributes(fullPath);
            return (attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) == 0 &&
                   IsAllowedName(Path.GetFileName(fullPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsAllowedName(string name)
    {
        if (DeniedExtensions.Contains(Path.GetExtension(name)) ||
            name.Equals("web.config", StringComparison.OrdinalIgnoreCase) ||
            AppSettingsName().IsMatch(name))
        {
            return false;
        }

        return true;
    }

    [GeneratedRegex("^appsettings(?:\\..+)?\\.json$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AppSettingsName();
}
