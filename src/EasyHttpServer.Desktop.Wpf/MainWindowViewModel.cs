using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Threading;
using EasyHttpServer.Core;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaximumTransferRows = 200;
    private static readonly Brush SuccessStatusBrush = CreateFrozenBrush(33, 110, 57);
    private static readonly Brush WarningStatusBrush = CreateFrozenBrush(122, 78, 0);
    private static readonly Brush RemoteStatusBrush = CreateFrozenBrush(154, 62, 0);
    private readonly IServerController _server;
    private readonly IFolderPickerService _folderPicker;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private readonly ISettingsStore _settings;
    private readonly ITransferLogWriter _transferLog;
    private readonly ILanServerProfileFactory? _lanProfiles;
    private readonly ILanNetworkCatalog _lanNetworkCatalog;
    private readonly ILanSessionSafetyMonitor _lanSafetyMonitor;
    private readonly IPublicIpResolver _publicIpResolver;
    private readonly IQrCodeRenderer _qrCodeRenderer;
    private readonly IOriginPortHistoryStore? _originHistory;
    private readonly IOriginPortAllocator? _originPortAllocator;
    private readonly ConcurrentQueue<TransferRecord> _pendingTransfers = new();
    private readonly BulkObservableCollection<TransferRecord> _transfers = [];
    private readonly DispatcherTimer _transferTimer;
    private ShareItemViewModel? _selectedShare;
    private int _port = 18080;
    private bool _isRunning;
    private string _publicUrl = "サーバー停止中";
    private string _statusText = "停止中";
    private Brush _statusBrush = Brushes.Gray;
    private string _serverStateText = "停止中";
    private Brush _serverStateBrush = Brushes.Gray;
    private bool _isLanEnabled;
    private bool _isVpnEnabled;
    private bool _isRemoteEnabled;
    private string _lanAccessCode = "（ペアリング停止中）";
    private string _certificateFingerprint = "（LAN停止中）";
    private LanSecurityMaterial? _activeLanSecurity;
    private LanPairingSession? _activePairingSession;
    private string _globalIpv4 = "未確認";
    private bool _isCheckingGlobalIpv4;
    private LanNetworkCandidate? _selectedLanNetwork;
    private LanNetworkCandidate? _selectedVpnNetwork;
    private ImageSource? _qrCodeImage;
    private bool _isWebsiteMode;
    private string _websiteRootPath = string.Empty;
    private string? _originStateError;

    public MainWindowViewModel(
        IServerController server,
        IFolderPickerService folderPicker,
        IClipboardService clipboard,
        IDialogService dialogs,
        IThemeService theme,
        ISettingsStore? settings = null,
        ITransferLogWriter? transferLog = null,
        ILanServerProfileFactory? lanProfiles = null,
        IPublicIpResolver? publicIpResolver = null,
        ILanNetworkCatalog? lanNetworkCatalog = null,
        ILanSessionSafetyMonitor? lanSafetyMonitor = null,
        IQrCodeRenderer? qrCodeRenderer = null,
        IOriginPortHistoryStore? originHistory = null,
        IOriginPortAllocator? originPortAllocator = null,
        string? applicationDirectory = null)
    {
        _server = server;
        _folderPicker = folderPicker;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _theme = theme;
        _settings = settings ?? new NullSettingsStore();
        _transferLog = transferLog ?? new NullTransferLogWriter();
        _lanProfiles = lanProfiles;
        _publicIpResolver = publicIpResolver ?? new UnavailablePublicIpResolver();
        _lanNetworkCatalog = lanNetworkCatalog ?? new LanNetworkCatalog();
        _lanSafetyMonitor = lanSafetyMonitor ?? new LanSessionSafetyMonitor();
        _qrCodeRenderer = qrCodeRenderer ?? new QrCodeRenderer();
        _originHistory = originHistory;
        _originPortAllocator = originPortAllocator;

        var loaded = _settings.Load();
        if (_originHistory is not null)
        {
            try
            {
                ApplicationStateInitializer.Initialize(loaded, _originHistory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
            {
                _originStateError = exception.Message;
            }
        }
        _port = loaded.Settings.Port;
        _isWebsiteMode = loaded.Settings.ContentMode == ContentMode.Website;
        _websiteRootPath = loaded.Settings.Website?.RootPath ?? string.Empty;
        _theme.SetClassic(loaded.Settings.IsClassic);
        foreach (var share in loaded.Settings.Shares)
        {
            Shares.Add(new ShareItemViewModel(share));
        }

        if (loaded.SourceMissing && !loaded.SourceInvalid && Shares.Count == 0 &&
            BundledGuide.TryCreate(applicationDirectory ?? AppContext.BaseDirectory) is { } guide)
        {
            Shares.Add(new ShareItemViewModel(guide));
            _statusText = "準備できました。「開始」を押すと説明書をこのPCで確認できます";
            _statusBrush = SuccessStatusBrush;
        }

        foreach (var candidate in _lanNetworkCatalog.GetCandidates())
        {
            if (candidate.NetworkKind == NetworkShareKind.Vpn)
            {
                VpnNetworks.Add(candidate);
            }
            else
            {
                LanNetworks.Add(candidate);
            }
        }

        _selectedLanNetwork = LanNetworks.FirstOrDefault();
        _selectedVpnNetwork = VpnNetworks.FirstOrDefault();

        if (loaded.Warning is not null)
        {
            _statusText = loaded.Warning;
            _statusBrush = WarningStatusBrush;
        }

        AddShareCommand = new RelayCommand(AddShare, () => CanEditFileSharingConfiguration, HandleUnexpectedCommandException);
        SelectWebsiteFolderCommand = new RelayCommand(
            SelectWebsiteFolder,
            () => CanEditConfiguration && IsWebsiteMode,
            HandleUnexpectedCommandException);
        RemoveShareCommand = new RelayCommand(
            RemoveShare,
            () => CanEditFileSharingConfiguration && SelectedShare is not null,
            HandleUnexpectedCommandException);
        StartCommand = new AsyncRelayCommand(
            StartAsync,
            () => !IsRunning && HasValidPublication &&
                   (!IsRemoteEnabled || IsLanEnabled || IsVpnEnabled) &&
                   (!IsLanEnabled || SelectedLanNetwork is not null) &&
                   (!IsVpnEnabled || SelectedVpnNetwork is not null),
            HandleUnexpectedCommandException);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning, HandleUnexpectedCommandException);
        CopyUrlCommand = new RelayCommand(
            CopyUrl,
            () => IsRunning && Uri.TryCreate(PublicUrl, UriKind.Absolute, out _),
            HandleUnexpectedCommandException);
        RenewPairingCodeCommand = new RelayCommand(
            RenewPairingCode,
            () => IsRunning && IsRemoteEnabled && _activePairingSession is not null,
            HandleUnexpectedCommandException);
        CheckGlobalIpv4Command = new AsyncRelayCommand(
            CheckGlobalIpv4Async,
            () => !IsCheckingGlobalIpv4,
            HandleUnexpectedCommandException);
        CopyGlobalIpv4Command = new RelayCommand(
            CopyGlobalIpv4,
            () => IPAddress.TryParse(GlobalIpv4, out var address) && address.AddressFamily == AddressFamily.InterNetwork,
            HandleUnexpectedCommandException);
        SettingsCommand = new RelayCommand(
            () => _dialogs.ShowSettings(this),
            errorHandler: HandleUnexpectedCommandException);
        AboutCommand = new RelayCommand(_dialogs.ShowAbout, errorHandler: HandleUnexpectedCommandException);
        ToggleThemeCommand = new RelayCommand(ToggleTheme, errorHandler: HandleUnexpectedCommandException);

        _server.TransferCompleted += OnTransferCompleted;
        _lanSafetyMonitor.StopRequired += OnLanSafetyStopRequired;
        _transferTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            FlushTransfers,
            Dispatcher.CurrentDispatcher);
        _transferTimer.Start();
    }

    public ObservableCollection<ShareItemViewModel> Shares { get; } = [];

    public ObservableCollection<TransferRecord> Transfers => _transfers;

    public ObservableCollection<LanNetworkCandidate> LanNetworks { get; } = [];

    public ObservableCollection<LanNetworkCandidate> VpnNetworks { get; } = [];

    public RelayCommand AddShareCommand { get; }

    public RelayCommand SelectWebsiteFolderCommand { get; }

    public RelayCommand RemoveShareCommand { get; }

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand StopCommand { get; }

    public RelayCommand CopyUrlCommand { get; }

    public RelayCommand RenewPairingCodeCommand { get; }

    public AsyncRelayCommand CheckGlobalIpv4Command { get; }

    public RelayCommand CopyGlobalIpv4Command { get; }

    public RelayCommand SettingsCommand { get; }

    public RelayCommand AboutCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public string ThemeButtonText => _theme.IsClassic ? "Modern" : "Classic 2005";

    public ShareItemViewModel? SelectedShare
    {
        get => _selectedShare;
        set
        {
            if (SetProperty(ref _selectedShare, value))
            {
                RemoveShareCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                StartCommand.RaiseCanExecuteChanged();
                NotifyStartRequirementChanged();
            }
        }
    }

    public bool IsWebsiteMode
    {
        get => _isWebsiteMode;
        set
        {
            if (CanEditConfiguration && SetProperty(ref _isWebsiteMode, value))
            {
                OnPropertyChanged(nameof(IsFileSharingMode));
                OnPropertyChanged(nameof(CanEditFileSharingConfiguration));
                OnPropertyChanged(nameof(CanEditWebsiteConfiguration));
                OnPropertyChanged(nameof(CanSelectLan));
                OnPropertyChanged(nameof(CanSelectVpn));
                RaiseCommandStates();
            }
        }
    }

    public bool IsFileSharingMode
    {
        get => !IsWebsiteMode;
        set
        {
            if (value)
            {
                IsWebsiteMode = false;
            }
        }
    }

    public string WebsiteRootPath
    {
        get => _websiteRootPath;
        private set
        {
            if (SetProperty(ref _websiteRootPath, value))
            {
                StartCommand.RaiseCanExecuteChanged();
                NotifyStartRequirementChanged();
            }
        }
    }

    public bool CanEditFileSharingConfiguration => CanEditConfiguration && IsFileSharingMode;

    public bool CanEditWebsiteConfiguration => CanEditConfiguration && IsWebsiteMode;

    private bool HasValidPublication => IsWebsiteMode
        ? !string.IsNullOrWhiteSpace(WebsiteRootPath) && Directory.Exists(WebsiteRootPath)
        : Shares.Count > 0 && Port is > 0 and <= IPEndPoint.MaxPort;

    public string StartRequirementText
    {
        get
        {
            if (IsRunning) return string.Empty;
            if (Port is <= 0 or > IPEndPoint.MaxPort) return "開始するには、ポートを1～65535にしてください";
            if (IsWebsiteMode && (string.IsNullOrWhiteSpace(WebsiteRootPath) || !Directory.Exists(WebsiteRootPath)))
                return "開始するには、Webサイトフォルダーを選んでください";
            if (IsFileSharingMode && Shares.Count == 0) return "開始するには、共有フォルダーを追加してください";
            if (IsRemoteEnabled && !IsLanEnabled && !IsVpnEnabled) return "開始するには、家庭内LANまたはVPNを選んでください";
            if (IsLanEnabled && SelectedLanNetwork is null) return "家庭内LANに使えるネットワークが見つかりません";
            if (IsVpnEnabled && SelectedVpnNetwork is null) return "VPNに使えるネットワークが見つかりません";
            return string.Empty;
        }
    }

    public bool HasStartRequirement => !string.IsNullOrEmpty(StartRequirementText);

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanEditConfiguration));
                OnPropertyChanged(nameof(CanEditFileSharingConfiguration));
                OnPropertyChanged(nameof(CanEditWebsiteConfiguration));
                RaiseCommandStates();
            }
        }
    }

    public bool CanEditConfiguration => !IsRunning;

    public bool IsLanEnabled
    {
        get => _isLanEnabled;
        set
        {
            if (CanEditConfiguration && SetProperty(ref _isLanEnabled, value))
            {
                if (value && !_isRemoteEnabled)
                {
                    _isRemoteEnabled = true;
                    OnPropertyChanged(nameof(IsRemoteEnabled));
                }

                if (value && _isVpnEnabled)
                {
                    _isVpnEnabled = false;
                    OnPropertyChanged(nameof(IsVpnEnabled));
                }

                OnPropertyChanged(nameof(IsRemoteEnabled));
                OnPropertyChanged(nameof(IsLocalOnly));
                OnPropertyChanged(nameof(IsRemoteConnectionSelected));
                OnPropertyChanged(nameof(RemoteConnectionSummary));
                StartCommand.RaiseCanExecuteChanged();
                NotifyStartRequirementChanged();
            }
        }
    }

    public bool IsVpnEnabled
    {
        get => _isVpnEnabled;
        set
        {
            if (CanEditConfiguration && SetProperty(ref _isVpnEnabled, value))
            {
                if (value && !_isRemoteEnabled)
                {
                    _isRemoteEnabled = true;
                    OnPropertyChanged(nameof(IsRemoteEnabled));
                }

                if (value && _isLanEnabled)
                {
                    _isLanEnabled = false;
                    OnPropertyChanged(nameof(IsLanEnabled));
                }

                OnPropertyChanged(nameof(IsRemoteEnabled));
                OnPropertyChanged(nameof(IsLocalOnly));
                OnPropertyChanged(nameof(IsRemoteConnectionSelected));
                OnPropertyChanged(nameof(RemoteConnectionSummary));
                StartCommand.RaiseCanExecuteChanged();
                NotifyStartRequirementChanged();
            }
        }
    }

    public bool IsRemoteEnabled
    {
        get => _isRemoteEnabled;
        set
        {
            if (!CanEditConfiguration || !SetProperty(ref _isRemoteEnabled, value))
            {
                return;
            }

            if (!value)
            {
                IsLanEnabled = false;
                IsVpnEnabled = false;
            }

            OnPropertyChanged(nameof(IsLocalOnly));
            OnPropertyChanged(nameof(IsRemoteConnectionSelected));
            OnPropertyChanged(nameof(RemoteConnectionSummary));
            StartCommand.RaiseCanExecuteChanged();
            NotifyStartRequirementChanged();
        }
    }

    public bool IsLocalOnly
    {
        get => !IsRemoteEnabled;
        set
        {
            if (value)
            {
                IsRemoteEnabled = false;
            }
        }
    }

    public string RemoteConnectionSummary => IsVpnEnabled
        ? $"接続方法: VPN（{SelectedVpnNetwork?.DisplayText ?? "未選択"}）"
        : IsLanEnabled
            ? $"接続方法: 家庭内LAN（{SelectedLanNetwork?.DisplayText ?? "未選択"}）"
            : IsRemoteEnabled
                ? SelectedLanNetwork is null && SelectedVpnNetwork is null
                    ? "利用可能な家庭内LANまたはVPNが見つかりません"
                    : "接続方法を選んでください"
                : "接続方法: このPCだけ";

    public bool IsRemoteConnectionSelected => IsRemoteEnabled && (IsLanEnabled || IsVpnEnabled);

    public bool CanSelectLan => CanEditConfiguration && SelectedLanNetwork is not null;

    public bool CanSelectVpn => CanEditConfiguration && SelectedVpnNetwork is not null;

    public string LanAccessCode
    {
        get => _lanAccessCode;
        private set => SetProperty(ref _lanAccessCode, value);
    }

    public string CertificateFingerprint
    {
        get => _certificateFingerprint;
        private set => SetProperty(ref _certificateFingerprint, value);
    }

    public string PublicUrl
    {
        get => _publicUrl;
        private set
        {
            if (SetProperty(ref _publicUrl, value))
            {
                CopyUrlCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ServerStateText
    {
        get => _serverStateText;
        private set => SetProperty(ref _serverStateText, value);
    }

    public Brush ServerStateBrush
    {
        get => _serverStateBrush;
        private set => SetProperty(ref _serverStateBrush, value);
    }

    public LanNetworkCandidate? SelectedLanNetwork
    {
        get => _selectedLanNetwork;
        set
        {
            if (CanEditConfiguration && SetProperty(ref _selectedLanNetwork, value))
            {
                OnPropertyChanged(nameof(RemoteConnectionSummary));
                OnPropertyChanged(nameof(CanSelectLan));
                StartCommand.RaiseCanExecuteChanged();
                NotifyStartRequirementChanged();
            }
        }
    }

    public LanNetworkCandidate? SelectedVpnNetwork
    {
        get => _selectedVpnNetwork;
        set
        {
            if (CanEditConfiguration && SetProperty(ref _selectedVpnNetwork, value))
            {
                OnPropertyChanged(nameof(RemoteConnectionSummary));
                OnPropertyChanged(nameof(CanSelectVpn));
                StartCommand.RaiseCanExecuteChanged();
                NotifyStartRequirementChanged();
            }
        }
    }

    public string GlobalIpv4
    {
        get => _globalIpv4;
        private set
        {
            if (SetProperty(ref _globalIpv4, value))
            {
                CopyGlobalIpv4Command.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCheckingGlobalIpv4
    {
        get => _isCheckingGlobalIpv4;
        private set
        {
            if (SetProperty(ref _isCheckingGlobalIpv4, value))
            {
                CheckGlobalIpv4Command.RaiseCanExecuteChanged();
            }
        }
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public ImageSource? QrCodeImage
    {
        get => _qrCodeImage;
        private set => SetProperty(ref _qrCodeImage, value);
    }

    public async Task ShutdownAsync()
    {
        _transferTimer.Stop();
        _server.TransferCompleted -= OnTransferCompleted;
        _lanSafetyMonitor.StopRequired -= OnLanSafetyStopRequired;
        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
            }
        }
        catch (Exception exception) when (CommandExceptionPolicy.IsRecoverable(exception))
        {
            HandleUnexpectedCommandException(exception);
        }
        finally
        {
            _lanSafetyMonitor.Disarm();
            ClearLanSecurity();
            ResetRemoteSelection();
            IsRunning = false;
            PublicUrl = "サーバー停止中";
            ServerStateText = "停止中";
            ServerStateBrush = Brushes.Gray;
        }

        await _server.DisposeAsync();
        _lanSafetyMonitor.Dispose();
        try
        {
            _originHistory?.ReserveFileSharingPort(Port);
            await _settings.SaveAsync(new ApplicationSettings(
                ApplicationSettings.CurrentSchemaVersion,
                Port,
                _theme.IsClassic,
                Shares.Select(item => item.Definition).ToArray(),
                IsWebsiteMode ? ContentMode.Website : ContentMode.FileSharing,
                string.IsNullOrWhiteSpace(WebsiteRootPath) ? null : WebsiteDefinition.Create(WebsiteRootPath)));
        }
        catch (Exception exception) when (CommandExceptionPolicy.IsRecoverable(exception))
        {
            HandleUnexpectedCommandException(exception);
        }
        await _transferLog.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(ShutdownAsync());

    private void AddShare()
    {
        var path = _folderPicker.PickFolder();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (Shares.Any(item => string.Equals(item.RootPath, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = "そのフォルダーは追加済みです";
            return;
        }

        var name = new DirectoryInfo(fullPath).Name;
        var baseSlug = ShareSlug.FromDisplayName(name);
        var slug = baseSlug;
        for (var suffix = 2; Shares.Any(item => item.Slug == slug); suffix++)
        {
            var suffixText = $"-{suffix}";
            var prefixLength = Math.Min(baseSlug.Length, ShareSlug.MaximumLength - suffixText.Length);
            slug = $"{baseSlug[..prefixLength].TrimEnd('-')}{suffixText}";
        }

        var item = new ShareItemViewModel(ShareDefinition.Create(name, slug, fullPath));
        Shares.Add(item);
        SelectedShare = item;
        StatusText = "共有フォルダーを追加しました";
        RaiseCommandStates();
    }

    private void SelectWebsiteFolder()
    {
        var path = _folderPicker.PickFolder();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var website = WebsiteDefinition.Create(path);
        var inspection = WebsitePreflight.Inspect(website.RootPath);
        if (!inspection.IsValidRoot)
        {
            StatusText = $"Webサイトフォルダーを選択できません: {inspection.Error}";
            StatusBrush = Brushes.Firebrick;
            return;
        }

        WebsiteRootPath = website.RootPath;
        StatusText = inspection.HasIndex
            ? "Webサイトフォルダーを選択しました"
            : "index.html / index.htm がありません。開始するとルートは404になります";
        StatusBrush = WarningStatusBrush;
    }

    private void RemoveShare()
    {
        if (SelectedShare is null)
        {
            return;
        }

        Shares.Remove(SelectedShare);
        SelectedShare = Shares.FirstOrDefault();
        StatusText = "共有フォルダーを削除しました";
        RaiseCommandStates();
    }

    private async Task StartAsync()
    {
        try
        {
            StatusText = "開始中…";
            StatusBrush = WarningStatusBrush;
            var definitions = Shares.Select(item => item.Definition).ToArray();
            if (_originStateError is not null)
            {
                throw new InvalidOperationException(_originStateError);
            }

            WebsiteDefinition? website = null;
            Publication publication;
            int effectivePort;
            if (IsWebsiteMode)
            {
                website = WebsiteDefinition.Create(WebsiteRootPath);
                if (!IsRemoteEnabled && !_dialogs.ConfirmWebsiteStart(new WebsiteStartConfirmation(website.RootPath, false)))
                {
                    StatusText = "Webサイト公開をキャンセルしました";
                    StatusBrush = Brushes.Gray;
                    return;
                }

                publication = new Publication.Website(website);
                effectivePort = _originPortAllocator?.AllocateAndRetire() ??
                    (IsRemoteEnabled
                        ? throw new InvalidOperationException("Webサイト用fresh port allocatorを初期化できませんでした")
                        : 0);
            }
            else
            {
                _originHistory?.ReserveFileSharingPort(Port);
                publication = new Publication.FileSharing(definitions);
                effectivePort = Port;
            }

            ServerOptions CreateOptions(IPAddress address, LanSecurityOptions? security) =>
                new(address, effectivePort, publication, security);

            LanSecurityOptions? lanSecurity = null;
            if (IsRemoteEnabled)
            {
                var kind = IsVpnEnabled ? NetworkShareKind.Vpn : NetworkShareKind.Lan;
                var candidate = IsVpnEnabled ? SelectedVpnNetwork : SelectedLanNetwork;
                if (candidate is null)
                {
                    throw new InvalidOperationException($"{(IsVpnEnabled ? "VPN" : "LAN")}公開するネットワークを選択してください");
                }
                if (!_dialogs.ConfirmLanStart(new LanStartConfirmation(
                        candidate.InterfaceName, candidate.Address, effectivePort, definitions, kind, website)))
                {
                    StatusText = $"{(IsVpnEnabled ? "VPN" : "LAN")}公開をキャンセルしました";
                    StatusBrush = Brushes.Gray;
                    return;
                }

                var profile = (_lanProfiles ?? throw new InvalidOperationException("LAN公開機能を初期化できませんでした")).Create(candidate.Address);
                _activeLanSecurity = profile.Security;
                _activePairingSession = new LanPairingSession();
                LanAccessCode = _activePairingSession.Code;
                CertificateFingerprint = FormatFingerprint(profile.Security.Fingerprint);
                lanSecurity = LanSecurityOptions.Pairing(
                    profile.Security.Certificate,
                    _activePairingSession,
                    kind);
            }

            var attempts = 0;
            while (true)
            {
                var address = IsRemoteEnabled
                    ? (IsVpnEnabled ? SelectedVpnNetwork! : SelectedLanNetwork!).Address
                    : IPAddress.Loopback;
                try
                {
                    await _server.StartAsync(CreateOptions(address, lanSecurity));
                    break;
                }
                catch (Exception exception) when (
                    IsWebsiteMode &&
                    _originPortAllocator is not null &&
                    IsAddressAlreadyInUse(exception) &&
                    ++attempts < OriginPortAllocator.MaximumBindAttempts)
                {
                    effectivePort = _originPortAllocator.AllocateAndRetire();
                }
            }
            PublicUrl = SelectDisplayedAddress(_server.BaseAddresses, IsRemoteEnabled).ToString();
            QrCodeImage = IsRemoteEnabled ? _qrCodeRenderer.RenderUrl(PublicUrl) : null;
            IsRunning = true;
            StatusText = IsWebsiteMode ? "Webサイト公開中（このPCのみ）" : "公開中（このPCのみ）";
            ServerStateText = IsWebsiteMode ? "WebサイトをこのPCに公開中" : "このPCに公開中";
            ServerStateBrush = SuccessStatusBrush;
            if (IsRemoteEnabled)
            {
                var label = IsVpnEnabled ? "VPN" : "LAN";
                var candidate = IsVpnEnabled ? SelectedVpnNetwork! : SelectedLanNetwork!;
                StatusText = $"{label}へHTTPS{(IsWebsiteMode ? " Webサイト" : string.Empty)}公開中（8桁ペアリングコード）";
                ServerStateText = $"{label}へ{(IsWebsiteMode ? "Webサイトを" : string.Empty)}公開中 — {candidate.Address}";
                ServerStateBrush = RemoteStatusBrush;
                _lanSafetyMonitor.Arm(candidate);
            }
            StatusBrush = SuccessStatusBrush;
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidOperationException or ArgumentException or CryptographicException)
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
            }

            IsRunning = false;
            PublicUrl = "サーバー停止中";
            QrCodeImage = null;
            StatusText = $"開始できません: {exception.Message}";
            StatusBrush = Brushes.Firebrick;
            ServerStateText = "停止中";
            ServerStateBrush = Brushes.Gray;
            _lanSafetyMonitor.Disarm();
            ClearLanSecurity();
            ResetRemoteSelection();
        }
    }

    private async Task StopAsync()
    {
        try
        {
            StatusText = "停止中…";
            StatusBrush = WarningStatusBrush;
            await _server.StopAsync();
            IsRunning = false;
            PublicUrl = "サーバー停止中";
            QrCodeImage = null;
            StatusText = "停止中";
            StatusBrush = Brushes.Gray;
            ServerStateText = "停止中";
            ServerStateBrush = Brushes.Gray;
            _lanSafetyMonitor.Disarm();
            ClearLanSecurity();
            ResetRemoteSelection();
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            StatusText = $"停止エラー: {exception.Message}";
            StatusBrush = Brushes.Firebrick;
        }
    }

    private void CopyUrl()
    {
        _clipboard.SetText(PublicUrl);
        StatusText = "URLをコピーしました";
    }

    private void RenewPairingCode()
    {
        var pairing = _activePairingSession ?? throw new InvalidOperationException("LAN共有が開始されていません");
        LanAccessCode = pairing.IssueNewCode();
        StatusText = "新しいペアリングコードを発行しました（5分・初回成功まで）";
        StatusBrush = SuccessStatusBrush;
    }

    private async Task CheckGlobalIpv4Async()
    {
        IsCheckingGlobalIpv4 = true;
        GlobalIpv4 = "確認中…";
        try
        {
            var address = await _publicIpResolver.ResolveIpv4Async();
            GlobalIpv4 = address.ToString();
            StatusText = "グローバルIPv4を確認しました（外部からの接続可否は未確認です）";
            StatusBrush = SuccessStatusBrush;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
        {
            GlobalIpv4 = "取得できませんでした";
            StatusText = $"グローバルIPv4を確認できません: {exception.Message}";
            StatusBrush = Brushes.Firebrick;
        }
        finally
        {
            IsCheckingGlobalIpv4 = false;
        }
    }

    private void CopyGlobalIpv4()
    {
        _clipboard.SetText(GlobalIpv4);
        StatusText = "グローバルIPv4をコピーしました";
    }

    private void ToggleTheme()
    {
        _theme.Toggle();
        OnPropertyChanged(nameof(ThemeButtonText));
    }

    private void OnTransferCompleted(object? sender, TransferRecord record)
    {
        _pendingTransfers.Enqueue(record);
        _transferLog.TryWrite(record);
    }

    private async void OnLanSafetyStopRequired(object? sender, string reason)
    {
        if (!IsRunning || !IsRemoteEnabled)
        {
            return;
        }

        try
        {
            await _server.StopAsync();
            IsRunning = false;
            PublicUrl = "サーバー停止中";
            QrCodeImage = null;
            ServerStateText = "安全のため停止";
            ServerStateBrush = Brushes.Firebrick;
            StatusText = $"共有を停止しました: {reason}";
            StatusBrush = Brushes.Firebrick;
            ClearLanSecurity();
            ResetRemoteSelection();
        }
        catch (Exception exception) when (CommandExceptionPolicy.IsRecoverable(exception))
        {
            HandleUnexpectedCommandException(exception);
        }
    }

    private void FlushTransfers(object? sender, EventArgs e)
    {
        FlushPendingTransfers();
    }

    internal void FlushPendingTransfers()
    {
        var pending = new List<TransferRecord>();
        while (_pendingTransfers.TryDequeue(out var record))
        {
            pending.Add(record);
        }

        if (pending.Count == 0)
        {
            return;
        }

        pending.Reverse();
        var snapshot = pending
            .Concat(_transfers)
            .Take(MaximumTransferRows)
            .ToArray();
        _transfers.ReplaceAll(snapshot);
    }

    private void HandleUnexpectedCommandException(Exception exception)
    {
        StatusText = $"予期しないエラー: {exception.Message}";
        StatusBrush = Brushes.Firebrick;
    }

    private void RaiseCommandStates()
    {
        AddShareCommand.RaiseCanExecuteChanged();
        SelectWebsiteFolderCommand.RaiseCanExecuteChanged();
        RemoveShareCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        CopyUrlCommand.RaiseCanExecuteChanged();
        RenewPairingCodeCommand.RaiseCanExecuteChanged();
        CheckGlobalIpv4Command.RaiseCanExecuteChanged();
        NotifyStartRequirementChanged();
    }

    private void NotifyStartRequirementChanged()
    {
        OnPropertyChanged(nameof(StartRequirementText));
        OnPropertyChanged(nameof(HasStartRequirement));
    }

    private void ResetRemoteSelection()
    {
        _isRemoteEnabled = false;
        _isLanEnabled = false;
        _isVpnEnabled = false;
        OnPropertyChanged(nameof(IsRemoteEnabled));
        OnPropertyChanged(nameof(IsLocalOnly));
        OnPropertyChanged(nameof(IsLanEnabled));
        OnPropertyChanged(nameof(IsVpnEnabled));
        OnPropertyChanged(nameof(IsRemoteConnectionSelected));
        OnPropertyChanged(nameof(RemoteConnectionSummary));
        StartCommand.RaiseCanExecuteChanged();
    }

    private void ClearLanSecurity()
    {
        _activePairingSession?.Dispose();
        _activePairingSession = null;
        _activeLanSecurity?.Dispose();
        _activeLanSecurity = null;
        LanAccessCode = "（ペアリング停止中）";
        CertificateFingerprint = "（LAN停止中）";
    }

    private static string FormatFingerprint(string value) =>
        string.Join(' ', Enumerable.Range(0, value.Length / 4).Select(index => value.Substring(index * 4, 4)));

    private static SolidColorBrush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Uri SelectDisplayedAddress(IReadOnlyList<Uri> addresses, bool preferLan)
    {
        if (preferLan)
        {
            var lanAddress = addresses.FirstOrDefault(address =>
                address.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
            if (lanAddress is not null)
            {
                return lanAddress;
            }
        }

        return addresses.Count > 0
            ? addresses[0]
            : throw new InvalidOperationException("The server did not publish a listening address.");
    }

    private static bool IsAddressAlreadyInUse(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
            {
                return true;
            }
        }

        return false;
    }
}
