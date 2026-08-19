namespace EasyHttpServer.Core;

public sealed record ShareDefinition(
    Guid Id,
    string Name,
    string Slug,
    string RootPath,
    bool DirectoryBrowsingEnabled = true,
    bool PreferIndexFile = true)
{
    public static ShareDefinition Create(string name, string slug, string rootPath) =>
        new(Guid.NewGuid(), name, slug, Path.GetFullPath(rootPath));
}
