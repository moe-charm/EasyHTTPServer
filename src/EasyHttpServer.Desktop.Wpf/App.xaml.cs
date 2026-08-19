using System.Configuration;
using System.Data;
using System.Windows;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private SingleInstanceGuard? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = SingleInstanceGuard.TryAcquire();
        if (_singleInstance is null)
        {
            MessageBox.Show(
                "EasyHTTPServer 2 はすでに起動しています。",
                "EasyHTTPServer 2",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var controller = new ServerController(new SharePathResolver());
        var historyStore = new JsonOriginPortHistoryStore(JsonOriginPortHistoryStore.DefaultFilePath);
        var viewModel = new MainWindowViewModel(
            controller,
            new FolderPickerService(),
            new ClipboardService(),
            new DialogService(),
            new ThemeService(),
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            new JsonLinesTransferLogWriter(JsonLinesTransferLogWriter.DefaultFilePath),
            new LanServerProfileFactory(),
            new AwsPublicIpResolver(),
            new LanNetworkCatalog(),
            new LanSessionSafetyMonitor(),
            originHistory: historyStore,
            originPortAllocator: new OriginPortAllocator(historyStore));
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}

