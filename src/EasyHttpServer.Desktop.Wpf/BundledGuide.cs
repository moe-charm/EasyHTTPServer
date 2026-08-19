using EasyHttpServer.Core;
using System.IO;

namespace EasyHttpServer.Desktop.Wpf;

internal static class BundledGuide
{
    internal const string DirectoryName = "Guide";
    internal const string DisplayName = "EasyHTTPServer の説明書";
    internal const string Slug = "easyhttpserver-guide";

    public static ShareDefinition? TryCreate(string applicationDirectory)
    {
        try
        {
            var root = Path.GetFullPath(Path.Combine(applicationDirectory, DirectoryName));
            if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "README.txt")))
            {
                return null;
            }

            return new ShareDefinition(
                Guid.NewGuid(),
                DisplayName,
                Slug,
                root,
                DirectoryBrowsingEnabled: true,
                PreferIndexFile: false);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
