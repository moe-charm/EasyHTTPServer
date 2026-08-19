using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using EasyHttpServer.Core;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf;

public interface IFolderPickerService
{
    string? PickFolder();
}

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "共有するフォルダーを選択",
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}

public interface IClipboardService
{
    void SetText(string value);
}

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string value) => System.Windows.Clipboard.SetText(value);
}

public interface IDialogService
{
    void ShowSettings(MainWindowViewModel viewModel);

    void ShowAbout();

    bool ConfirmLanStart(LanStartConfirmation confirmation);

    bool ConfirmWebsiteStart(WebsiteStartConfirmation confirmation) => true;
}

public sealed record LanStartConfirmation(
    string NetworkName,
    IPAddress Address,
    int Port,
    IReadOnlyList<ShareDefinition> Shares,
    NetworkShareKind NetworkKind = NetworkShareKind.Lan,
    WebsiteDefinition? Website = null);

public sealed record WebsiteStartConfirmation(string RootPath, bool IsRemote, Uri? ProposedAddress = null);

public sealed class DialogService : IDialogService
{
    public void ShowSettings(MainWindowViewModel viewModel)
    {
        var window = new SettingsWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow,
        };
        window.ShowDialog();
    }

    public void ShowAbout()
    {
        var window = new AboutWindow
        {
            Owner = System.Windows.Application.Current.MainWindow,
        };
        window.ShowDialog();
    }

    public bool ConfirmLanStart(LanStartConfirmation confirmation)
    {
        var shares = confirmation.Website is null
            ? string.Join(Environment.NewLine, confirmation.Shares.Select(share => $"・{share.Name}: {share.RootPath}"))
            : $"・Webサイト: {confirmation.Website.RootPath}";
        var networkLabel = confirmation.NetworkKind == NetworkShareKind.Vpn ? "VPN" : "LAN";
        var websiteWarning = confirmation.Website is null
            ? string.Empty
            : "\nHTMLとJavaScriptは閲覧端末で実行されます。公開専用で、内容を信頼できるフォルダーだけを選んでください。\n" +
              "ディレクトリ一覧、CGI、アップロードは有効になりません。\n";
        var message = $"次の範囲を{networkLabel}へ公開します。\n\n" +
                      $"ネットワーク: {confirmation.NetworkName}\n" +
                      $"待ち受け: https://{confirmation.Address}:{confirmation.Port}/\n" +
                      $"認証: HTTPS上の8桁ペアリングコード（5分・初回成功まで）\n\n" +
                      $"公開する共有:\n{shares}\n{websiteWarning}\n" +
                      "Windows Firewallやルーター設定は変更しません。開始しますか？";
        return System.Windows.MessageBox.Show(
                   message,
                   $"{networkLabel}公開の確認",
                   System.Windows.MessageBoxButton.OKCancel,
                   System.Windows.MessageBoxImage.Warning,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.OK;
    }

    public bool ConfirmWebsiteStart(WebsiteStartConfirmation confirmation)
    {
        var scope = confirmation.IsRemote ? "LAN/VPNの閲覧端末" : "このPCのブラウザー";
        var message = "選択したフォルダーを静的Webサイトとして公開します。\n\n" +
                      $"サイトルート: {confirmation.RootPath}\n" +
                      $"実行場所: {scope}\n\n" +
                      "HTMLとJavaScriptはブラウザーで実行されます。公開専用で、内容を信頼できるフォルダーだけを選んでください。\n" +
                      "ディレクトリ一覧、CGI、アップロードは有効になりません。開始しますか？";
        return System.Windows.MessageBox.Show(
                   message,
                   "Webサイト公開の確認",
                   System.Windows.MessageBoxButton.OKCancel,
                   System.Windows.MessageBoxImage.Warning,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.OK;
    }
}

public interface IThemeService
{
    bool IsClassic { get; }

    void SetClassic(bool isClassic);

    void Toggle();
}

public sealed class ThemeService : IThemeService
{
    public bool IsClassic { get; private set; }

    public void SetClassic(bool isClassic)
    {
        IsClassic = isClassic;
        var resources = System.Windows.Application.Current.Resources.MergedDictionaries;
        resources.Clear();
        resources.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri(IsClassic ? "Themes/Classic.xaml" : "Themes/Modern.xaml", UriKind.Relative),
        });
    }

    public void Toggle() => SetClassic(!IsClassic);
}

public sealed record LanNetworkCandidate(
    string InterfaceId,
    string InterfaceName,
    IPAddress Address,
    NetworkShareKind NetworkKind = NetworkShareKind.Lan)
{
    public string DisplayText => $"{InterfaceName} — {Address}";
}

public interface ILanNetworkCatalog
{
    IReadOnlyList<LanNetworkCandidate> GetCandidates();
}

public sealed class LanNetworkCatalog : ILanNetworkCatalog
{
    public IReadOnlyList<LanNetworkCandidate> GetCandidates() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(item => item.OperationalStatus == OperationalStatus.Up &&
                       item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .OrderByDescending(item => item.GetIPProperties().GatewayAddresses.Count > 0)
        .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
        .SelectMany(item => item.GetIPProperties().UnicastAddresses
            .Select(address => new LanNetworkCandidate(
                item.Id,
                item.Name,
                address.Address,
                NetworkShareCandidateRules.IsVpnInterfaceType(item.NetworkInterfaceType)
                    ? NetworkShareKind.Vpn
                    : NetworkShareKind.Lan)))
        .Where(candidate => candidate.Address.AddressFamily == AddressFamily.InterNetwork &&
                            (candidate.NetworkKind == NetworkShareKind.Vpn
                                ? VpnNetworkAddress.IsAllowedIpv4(candidate.Address)
                                : LanNetworkAddress.IsPrivateIpv4(candidate.Address)))
        .ToArray();
}

public static class NetworkShareCandidateRules
{
    private const int PropVirtualInterfaceType = 53;

    public static bool IsVpnInterfaceType(NetworkInterfaceType interfaceType) =>
        interfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp ||
        (int)interfaceType == PropVirtualInterfaceType;
}

public sealed record LanServerProfile(IPAddress Address, LanSecurityMaterial Security);

public interface ILanServerProfileFactory
{
    LanServerProfile Create(IPAddress address);
}

public sealed class LanServerProfileFactory : ILanServerProfileFactory
{
    public LanServerProfile Create(IPAddress address) => new(address, LanSecurityMaterial.Create(address));
}

public interface ILanSessionSafetyMonitor : IDisposable
{
    event EventHandler<string>? StopRequired;

    void Arm(LanNetworkCandidate candidate);

    void Disarm();
}

public sealed class LanSessionSafetyMonitor : ILanSessionSafetyMonitor
{
    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private readonly object _gate = new();
    private LanNetworkCandidate? _candidate;
    private bool _disposed;

    public LanSessionSafetyMonitor()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public event EventHandler<string>? StopRequired;

    public void Arm(LanNetworkCandidate candidate)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _candidate = candidate;
        }
    }

    public void Disarm()
    {
        lock (_gate)
        {
            _candidate = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _candidate = null;
        }

        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        LanNetworkCandidate? candidate;
        lock (_gate)
        {
            candidate = _candidate;
        }

        if (candidate is null)
        {
            return;
        }

        var stillPresent = NetworkInterface.GetAllNetworkInterfaces().Any(item =>
            item.Id == candidate.InterfaceId &&
            item.OperationalStatus == OperationalStatus.Up &&
            item.GetIPProperties().UnicastAddresses.Any(address => address.Address.Equals(candidate.Address)));
        if (!stillPresent)
        {
            RaiseStopRequired("選択したネットワークまたはIPアドレスが変更されました");
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable)
        {
            RaiseStopRequired("ネットワークが利用できなくなりました");
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            RaiseStopRequired("PCがスリープへ移行しました");
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            RaiseStopRequired("Windowsがロックされました");
        }
    }

    private void RaiseStopRequired(string reason)
    {
        lock (_gate)
        {
            if (_candidate is null)
            {
                return;
            }

            _candidate = null;
        }

        if (_synchronizationContext is null)
        {
            StopRequired?.Invoke(this, reason);
            return;
        }

        _synchronizationContext.Post(_ => StopRequired?.Invoke(this, reason), null);
    }
}
