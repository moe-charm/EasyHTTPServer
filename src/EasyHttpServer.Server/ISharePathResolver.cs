using EasyHttpServer.Core;

namespace EasyHttpServer.Server;

public interface ISharePathResolver
{
    PathResolution Resolve(ShareDefinition share, string? untrustedRelativePath);
}
