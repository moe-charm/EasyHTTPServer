using System.Net;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyHttpServer.Core;

namespace EasyHttpServer.Desktop.Wpf;

public sealed record ApplicationSettings(
    int SchemaVersion,
    int Port,
    bool IsClassic,
    IReadOnlyList<ShareDefinition> Shares,
    ContentMode ContentMode,
    WebsiteDefinition? Website)
{
    public const int CurrentSchemaVersion = 2;

    public ApplicationSettings(int schemaVersion, int port, bool isClassic, IReadOnlyList<ShareDefinition> shares)
        : this(schemaVersion, port, isClassic, shares, ContentMode.FileSharing, null)
    {
    }

    public static ApplicationSettings Default { get; } =
        new(CurrentSchemaVersion, 18080, false, [], ContentMode.FileSharing, null);
}

public sealed record SettingsLoadResult(
    ApplicationSettings Settings,
    string? Warning = null,
    int? SourceSchemaVersion = null,
    bool SourceMissing = false,
    bool SourceInvalid = false);

public interface ISettingsStore
{
    SettingsLoadResult Load();

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}

public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EasyHTTPServer",
        "settings.json");

    public SettingsLoadResult Load()
    {
        if (!File.Exists(filePath))
        {
            return new(ApplicationSettings.Default, SourceMissing: true);
        }

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(filePath), JsonOptions);
            if (document is null || document.SchemaVersion is not (1 or ApplicationSettings.CurrentSchemaVersion))
            {
                return new(
                    ApplicationSettings.Default,
                    "設定ファイルの形式が未対応のため、既定値で起動しました",
                    SourceSchemaVersion: document?.SchemaVersion,
                    SourceInvalid: true);
            }

            var savedPort = document.SchemaVersion == 1 ? document.Port : document.FileSharingPort;
            var port = savedPort is > 0 and <= IPEndPoint.MaxPort ? savedPort.Value : 18080;
            var shares = new List<ShareDefinition>();
            var skipped = 0;
            foreach (var saved in document.Shares ?? [])
            {
                if (string.IsNullOrWhiteSpace(saved.Name) || !ShareSlug.IsValid(saved.Slug) ||
                    string.IsNullOrWhiteSpace(saved.RootPath) || !Directory.Exists(saved.RootPath) ||
                    shares.Any(item => item.Slug == saved.Slug ||
                        string.Equals(item.RootPath, saved.RootPath, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }

                shares.Add(new ShareDefinition(
                    saved.Id == Guid.Empty ? Guid.NewGuid() : saved.Id,
                    saved.Name.Trim(),
                    saved.Slug,
                    Path.GetFullPath(saved.RootPath),
                    saved.DirectoryBrowsingEnabled,
                    saved.PreferIndexFile));
            }

            var mode = document.SchemaVersion == 1 ? ContentMode.FileSharing : document.ContentMode;
            var website = document.SchemaVersion == 1 ? null : RestoreWebsite(document.Website);
            if (mode == ContentMode.Website && website is null)
            {
                mode = ContentMode.FileSharing;
                skipped++;
            }

            var warning = skipped > 0 ? $"利用できない公開設定を{skipped}件復元しませんでした" : null;
            return new(new(
                ApplicationSettings.CurrentSchemaVersion,
                port,
                document.IsClassic,
                shares,
                mode,
                website), warning, SourceSchemaVersion: document.SchemaVersion);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(
                ApplicationSettings.Default,
                "設定ファイルを読み込めないため、既定値で起動しました",
                SourceInvalid: true);
        }
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new InvalidOperationException("Settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var document = SettingsDocument.From(settings);
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static WebsiteDefinition? RestoreWebsite(SavedWebsite? saved)
    {
        if (saved is null || string.IsNullOrWhiteSpace(saved.RootPath) ||
            saved.RootPath.StartsWith("\\\\", StringComparison.Ordinal) ||
            saved.RootPath.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            saved.RootPath.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(saved.RootPath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root) || new DriveInfo(root).DriveType == DriveType.Network ||
                !Directory.Exists(fullPath))
            {
                return null;
            }

            for (var current = new DirectoryInfo(fullPath); current is not null; current = current.Parent)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return null;
                }
            }

            return WebsiteDefinition.Create(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record SettingsDocument(
        int SchemaVersion,
        int? Port,
        int? FileSharingPort,
        bool IsClassic,
        IReadOnlyList<SavedShare>? Shares,
        ContentMode ContentMode,
        SavedWebsite? Website)
    {
        public static SettingsDocument From(ApplicationSettings settings) => new(
            ApplicationSettings.CurrentSchemaVersion,
            null,
            settings.Port,
            settings.IsClassic,
            settings.Shares.Select(SavedShare.From).ToArray(),
            settings.ContentMode,
            settings.Website is null ? null : new SavedWebsite(settings.Website.RootPath));
    }

    private sealed record SavedWebsite(string RootPath);

    private sealed record SavedShare(
        Guid Id,
        string Name,
        string Slug,
        string RootPath,
        bool DirectoryBrowsingEnabled,
        bool PreferIndexFile)
    {
        public static SavedShare From(ShareDefinition share) => new(
            share.Id, share.Name, share.Slug, share.RootPath,
            share.DirectoryBrowsingEnabled, share.PreferIndexFile);
    }
}

internal sealed class NullSettingsStore : ISettingsStore
{
    public SettingsLoadResult Load() => new(ApplicationSettings.Default);

    public Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
