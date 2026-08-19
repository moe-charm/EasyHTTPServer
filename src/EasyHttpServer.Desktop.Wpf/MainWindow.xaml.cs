using System.ComponentModel;
using System.Windows;

namespace EasyHttpServer.Desktop.Wpf;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _shutdownStarted;
    private bool _shutdownComplete;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (!_shutdownComplete)
        {
            e.Cancel = true;
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            await _viewModel.ShutdownAsync();
            _shutdownComplete = true;
            Close();
            return;
        }

        base.OnClosing(e);
    }
}
