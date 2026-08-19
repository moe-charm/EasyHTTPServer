using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf;

public static class ApplicationStateInitializer
{
    public static void Initialize(SettingsLoadResult settings, IOriginPortHistoryStore historyStore)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(historyStore);

        if (!historyStore.Exists)
        {
            if (settings.SourceMissing || settings.SourceSchemaVersion == 1)
            {
                historyStore.Create(settings.Settings.Port);
                return;
            }

            throw new InvalidOperationException(
                "origin port履歴が失われています。公開を開始せず、設定と履歴を対で復旧してください。");
        }

        _ = historyStore.Load();
        historyStore.ReserveFileSharingPort(settings.Settings.Port);
    }
}
