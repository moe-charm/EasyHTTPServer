namespace EasyHttpServer.Server;

public enum PathResolutionStatus
{
    Success,
    InvalidPath,
    OutsideRoot,
    ReparsePoint,
    NotFound,
}

public sealed record PathResolution(
    PathResolutionStatus Status,
    string? FullPath = null,
    bool IsDirectory = false)
{
    public bool IsSuccess => Status == PathResolutionStatus.Success;
}
