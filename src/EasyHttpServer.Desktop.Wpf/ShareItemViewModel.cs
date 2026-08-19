using EasyHttpServer.Core;

namespace EasyHttpServer.Desktop.Wpf;

public sealed class ShareItemViewModel(ShareDefinition definition)
{
    public ShareDefinition Definition { get; } = definition;

    public string Name => Definition.Name;

    public string Slug => Definition.Slug;

    public string RootPath => Definition.RootPath;
}
