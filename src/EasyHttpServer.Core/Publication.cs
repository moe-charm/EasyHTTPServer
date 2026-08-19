namespace EasyHttpServer.Core;

public enum ContentMode
{
    FileSharing,
    Website,
}

public abstract record Publication
{
    private Publication()
    {
    }

    public sealed record FileSharing(IReadOnlyList<ShareDefinition> Shares) : Publication
    {
        public FileSharing(params ShareDefinition[] shares) : this((IReadOnlyList<ShareDefinition>)shares)
        {
        }
    }

    public sealed record Website(WebsiteDefinition Definition) : Publication;
}

public sealed record WebsiteDefinition(string RootPath)
{
    public static WebsiteDefinition Create(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return new(Path.GetFullPath(rootPath));
    }
}
