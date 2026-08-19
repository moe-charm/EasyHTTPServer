using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using EasyHttpServer.Core;
using EasyHttpServer.Desktop.Wpf;
using EasyHttpServer.Server;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task JapaneseFoldersReceiveStableUniqueSlugs()
    {
        var root = Path.GetTempPath();
        var picker = new FakeFolderPicker(
            Path.Combine(root, "first", "資料"),
            Path.Combine(root, "second", "資料"));
        await using var fixture = new ViewModelFixture(picker: picker);

        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.AddShareCommand.Execute(null);

        Assert.Matches("^share-[0-9a-f]{8}$", fixture.ViewModel.Shares[0].Slug);
        Assert.Equal($"{fixture.ViewModel.Shares[0].Slug}-2", fixture.ViewModel.Shares[1].Slug);
    }

    [Fact]
    public async Task CommandsFollowServerState()
    {
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(picker: picker);
        fixture.ViewModel.AddShareCommand.Execute(null);

        Assert.True(fixture.ViewModel.StartCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.StopCommand.CanExecute(null));

        await fixture.ViewModel.StartCommand.ExecuteAsync();

        Assert.True(fixture.ViewModel.IsRunning);
        Assert.False(fixture.ViewModel.StartCommand.CanExecute(null));
        Assert.True(fixture.ViewModel.StopCommand.CanExecute(null));

        await fixture.ViewModel.StopCommand.ExecuteAsync();

        Assert.False(fixture.ViewModel.IsRunning);
        Assert.True(fixture.ViewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnexpectedStartExceptionBecomesStatusMessage()
    {
        var server = new FakeServerController { StartException = new FormatException("injected") };
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(server, picker);
        fixture.ViewModel.AddShareCommand.Execute(null);

        await fixture.ViewModel.StartCommand.ExecuteAsync();

        Assert.Equal("予期しないエラー: injected", fixture.ViewModel.StatusText);
        Assert.False(fixture.ViewModel.IsRunning);
        Assert.True(fixture.ViewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task TransferHistoryKeepsNewestTwoHundredRows()
    {
        var server = new FakeServerController();
        await using var fixture = new ViewModelFixture(server);
        for (var index = 0; index < 205; index++)
        {
            server.RaiseTransfer(index);
        }

        var notifications = 0;
        fixture.ViewModel.Transfers.CollectionChanged += (_, args) =>
        {
            Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action);
            notifications++;
        };

        fixture.ViewModel.FlushPendingTransfers();

        Assert.Equal(200, fixture.ViewModel.Transfers.Count);
        Assert.Equal("/204", fixture.ViewModel.Transfers[0].Path);
        Assert.Equal("/5", fixture.ViewModel.Transfers[^1].Path);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task ClipboardFailureBecomesStatusMessage()
    {
        var clipboard = new FakeClipboard { Exception = new InvalidOperationException("clipboard busy") };
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(picker: picker, clipboard: clipboard);
        fixture.ViewModel.AddShareCommand.Execute(null);
        await fixture.ViewModel.StartCommand.ExecuteAsync();

        fixture.ViewModel.CopyUrlCommand.Execute(null);

        Assert.Equal("予期しないエラー: clipboard busy", fixture.ViewModel.StatusText);
        Assert.True(fixture.ViewModel.IsRunning);
    }

    [Fact]
    public async Task StartRollsBackWhenServerPublishesNoAddress()
    {
        var server = new FakeServerController { OmitBaseAddress = true };
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(server, picker);
        fixture.ViewModel.AddShareCommand.Execute(null);

        await fixture.ViewModel.StartCommand.ExecuteAsync();

        Assert.False(server.IsRunning);
        Assert.Equal(1, server.StopCallCount);
        Assert.False(fixture.ViewModel.IsRunning);
        Assert.Equal("サーバー停止中", fixture.ViewModel.PublicUrl);
        Assert.StartsWith("開始できません:", fixture.ViewModel.StatusText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public async Task StartCommandValidatesPortRange(int port, bool expected)
    {
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(picker: picker);
        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.Port = port;

        Assert.Equal(expected, fixture.ViewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartRequirementExplainsWhatIsMissing()
    {
        await using var fixture = new ViewModelFixture();

        Assert.Equal("開始するには、共有フォルダーを追加してください", fixture.ViewModel.StartRequirementText);
        Assert.True(fixture.ViewModel.HasStartRequirement);

        fixture.ViewModel.IsRemoteEnabled = true;
        Assert.Equal("開始するには、共有フォルダーを追加してください", fixture.ViewModel.StartRequirementText);
    }

    [Fact]
    public async Task SavedEmptySettingsDoNotRestoreBundledGuide()
    {
        var settings = new FakeSettingsStore(new SettingsLoadResult(ApplicationSettings.Default));
        await using var fixture = new ViewModelFixture(settings: settings);

        Assert.Empty(fixture.ViewModel.Shares);
    }

    [Fact]
    public async Task FirstRunAddsBundledGuideAndMakesStartAvailable()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var guide = Directory.CreateDirectory(Path.Combine(root, BundledGuide.DirectoryName)).FullName;
        await File.WriteAllTextAsync(Path.Combine(guide, "README.txt"), "guide");
        try
        {
            var settings = new FakeSettingsStore(new SettingsLoadResult(ApplicationSettings.Default, SourceMissing: true));
            await using var fixture = new ViewModelFixture(settings: settings, applicationDirectory: root);

            var share = Assert.Single(fixture.ViewModel.Shares).Definition;
            Assert.Equal(BundledGuide.DisplayName, share.Name);
            Assert.True(fixture.ViewModel.StartCommand.CanExecute(null));
            Assert.Contains("開始", fixture.ViewModel.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SettingsAreRestoredAndSavedOnShutdown()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var share = ShareDefinition.Create("docs", "docs", root);
            var settings = new FakeSettingsStore(new SettingsLoadResult(
                new ApplicationSettings(1, 19090, true, [share])));
            await using (var fixture = new ViewModelFixture(settings: settings))
            {
                Assert.Equal(19090, fixture.ViewModel.Port);
                Assert.Equal("Modern", fixture.ViewModel.ThemeButtonText);
                Assert.Equal(share, Assert.Single(fixture.ViewModel.Shares).Definition);
            }

            Assert.NotNull(settings.Saved);
            Assert.Equal(19090, settings.Saved.Port);
            Assert.True(settings.Saved.IsClassic);
            Assert.Equal(share, Assert.Single(settings.Saved.Shares));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task WebsiteModeSelectsOneRootUsesFreshPortAndPreservesFileShares()
    {
        var shareRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var websiteRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        await File.WriteAllTextAsync(Path.Combine(websiteRoot, "index.html"), "site");
        try
        {
            var share = ShareDefinition.Create("files", "files", shareRoot);
            var settings = new FakeSettingsStore(new SettingsLoadResult(new ApplicationSettings(
                ApplicationSettings.CurrentSchemaVersion,
                18080,
                false,
                [share],
                ContentMode.FileSharing,
                Website: null)));
            var picker = new FakeFolderPicker(websiteRoot);
            var dialogs = new FakeDialogs();
            await using var fixture = new ViewModelFixture(
                picker: picker,
                settings: settings,
                dialogs: dialogs,
                originPortAllocator: new FakeOriginPortAllocator(51234));

            fixture.ViewModel.IsWebsiteMode = true;
            fixture.ViewModel.SelectWebsiteFolderCommand.Execute(null);
            await fixture.ViewModel.StartCommand.ExecuteAsync();

            Assert.True(fixture.ViewModel.IsRunning);
            Assert.True(fixture.Server.LastOptions?.IsWebsite);
            Assert.Equal(websiteRoot, fixture.Server.LastOptions?.Website?.RootPath);
            Assert.Equal(51234, fixture.Server.LastOptions?.Port);
            Assert.Equal("http://127.0.0.1:51234/", fixture.ViewModel.PublicUrl);
            Assert.Equal(1, dialogs.WebsiteConfirmationCount);

            await fixture.ViewModel.StopCommand.ExecuteAsync();
            fixture.ViewModel.IsFileSharingMode = true;
            Assert.Equal(share, Assert.Single(fixture.ViewModel.Shares).Definition);
        }
        finally
        {
            Directory.Delete(shareRoot, true);
            Directory.Delete(websiteRoot, true);
        }
    }

    [Fact]
    public async Task CompletedTransfersAreLoggedAndWriterIsDisposed()
    {
        var server = new FakeServerController();
        var log = new FakeTransferLogWriter();
        await using (var fixture = new ViewModelFixture(server, transferLog: log))
        {
            server.RaiseTransfer(42);
            Assert.Equal("/42", Assert.Single(log.Records).Path);
        }

        Assert.True(log.IsDisposed);
    }

    [Fact]
    public async Task ShutdownStopsListenerBeforeSavingSettings()
    {
        var callOrder = new List<string>();
        var server = new FakeServerController { CallOrder = callOrder };
        var settings = new FakeSettingsStore(new SettingsLoadResult(ApplicationSettings.Default), callOrder);
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        var fixture = new ViewModelFixture(server, picker, settings: settings);
        fixture.ViewModel.AddShareCommand.Execute(null);
        await fixture.ViewModel.StartCommand.ExecuteAsync();

        await fixture.DisposeAsync();

        Assert.True(callOrder.IndexOf("stop") < callOrder.IndexOf("save"));
        Assert.True(callOrder.IndexOf("dispose-server") < callOrder.IndexOf("save"));
    }

    [Fact]
    public async Task LanSessionUsesHttpsCredentialsAndClearsSecretsOnStop()
    {
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        var profileFactory = new FakeLanProfileFactory();
        await using var fixture = new ViewModelFixture(picker: picker, lanProfiles: profileFactory);
        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.IsLanEnabled = true;

        await fixture.ViewModel.StartCommand.ExecuteAsync();

        Assert.NotNull(fixture.Server.LastOptions?.LanSecurity);
        Assert.StartsWith("https://", fixture.ViewModel.PublicUrl, StringComparison.Ordinal);
        Assert.Equal(2, fixture.Server.BaseAddresses.Count);
        Assert.Contains(fixture.Server.BaseAddresses, address => address.Scheme == Uri.UriSchemeHttp);
        Assert.Contains(fixture.Server.BaseAddresses, address => address.Scheme == Uri.UriSchemeHttps);
        Assert.NotNull(fixture.Server.LastOptions?.LanSecurity?.PairingSession);
        Assert.Matches("^[0-9]{8}$", fixture.ViewModel.LanAccessCode);
        Assert.Contains(' ', fixture.ViewModel.CertificateFingerprint);
        Assert.Contains("LAN", fixture.ViewModel.StatusText, StringComparison.Ordinal);

        await fixture.ViewModel.StopCommand.ExecuteAsync();

        Assert.Equal("（ペアリング停止中）", fixture.ViewModel.LanAccessCode);
        Assert.Equal("（LAN停止中）", fixture.ViewModel.CertificateFingerprint);
    }

    [Fact]
    public async Task RunningLanSessionCanIssueANewPairingCode()
    {
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(picker: picker);
        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.IsLanEnabled = true;
        await fixture.ViewModel.StartCommand.ExecuteAsync();
        var first = fixture.ViewModel.LanAccessCode;

        fixture.ViewModel.RenewPairingCodeCommand.Execute(null);

        Assert.Matches("^[0-9]{8}$", fixture.ViewModel.LanAccessCode);
        Assert.NotEqual(first, fixture.ViewModel.LanAccessCode);
        Assert.Contains("5分", fixture.ViewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VpnSessionUsesSelectedVirtualAdapterAndExcludesLanMode()
    {
        var lan = new LanNetworkCandidate("lan", "Ethernet", IPAddress.Parse("192.168.1.20"));
        var vpn = new LanNetworkCandidate(
            "vpn", "Private tunnel", IPAddress.Parse("100.100.100.20"), NetworkShareKind.Vpn);
        var safety = new FakeLanSafetyMonitor();
        var dialogs = new FakeDialogs();
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(
            picker: picker,
            dialogs: dialogs,
            lanNetworks: new FakeLanNetworkCatalog(lan, vpn),
            lanSafetyMonitor: safety);
        fixture.ViewModel.AddShareCommand.Execute(null);

        Assert.Equal(lan, Assert.Single(fixture.ViewModel.LanNetworks));
        Assert.Equal(vpn, Assert.Single(fixture.ViewModel.VpnNetworks));
        fixture.ViewModel.IsRemoteEnabled = true;
        Assert.False(fixture.ViewModel.StartCommand.CanExecute(null));
        fixture.ViewModel.IsVpnEnabled = true;
        Assert.False(fixture.ViewModel.IsLanEnabled);
        Assert.True(fixture.ViewModel.StartCommand.CanExecute(null));

        await fixture.ViewModel.StartCommand.ExecuteAsync();

        Assert.Equal(NetworkShareKind.Vpn, fixture.Server.LastOptions?.LanSecurity?.NetworkKind);
        Assert.Equal(vpn.Address, fixture.Server.LastOptions?.ListenAddress);
        Assert.Equal(NetworkShareKind.Vpn, dialogs.LastConfirmation?.NetworkKind);
        Assert.Equal(vpn, safety.ArmedCandidate);
        Assert.Contains("VPNへ公開中", fixture.ViewModel.ServerStateText, StringComparison.Ordinal);
        Assert.StartsWith("https://", fixture.ViewModel.PublicUrl, StringComparison.Ordinal);

        await fixture.ViewModel.StopCommand.ExecuteAsync();

        Assert.True(fixture.ViewModel.IsLocalOnly);
        Assert.False(fixture.ViewModel.IsRemoteEnabled);
        Assert.False(fixture.ViewModel.IsVpnEnabled);
    }

    [Fact]
    public async Task RemoteScopeRequiresExplicitConnectionChoiceAndCanReturnToLocalOnly()
    {
        var lan = new LanNetworkCandidate("lan", "Ethernet", IPAddress.Parse("192.168.1.20"));
        var vpn = new LanNetworkCandidate(
            "vpn", "Tailscale", IPAddress.Parse("100.100.100.20"), NetworkShareKind.Vpn);
        await using var fixture = new ViewModelFixture(
            lanNetworks: new FakeLanNetworkCatalog(lan, vpn));

        fixture.ViewModel.IsRemoteEnabled = true;

        Assert.False(fixture.ViewModel.IsVpnEnabled);
        Assert.False(fixture.ViewModel.IsLanEnabled);
        Assert.Equal("接続方法を選んでください", fixture.ViewModel.RemoteConnectionSummary);

        fixture.ViewModel.IsVpnEnabled = true;

        Assert.True(fixture.ViewModel.IsRemoteConnectionSelected);
        Assert.Contains("VPN", fixture.ViewModel.RemoteConnectionSummary, StringComparison.Ordinal);
        Assert.Contains("Tailscale", fixture.ViewModel.RemoteConnectionSummary, StringComparison.Ordinal);

        fixture.ViewModel.IsLocalOnly = true;

        Assert.False(fixture.ViewModel.IsRemoteEnabled);
        Assert.True(fixture.ViewModel.IsLocalOnly);
        Assert.Equal("接続方法: このPCだけ", fixture.ViewModel.RemoteConnectionSummary);
    }

    [Fact]
    public async Task RemoteScopeWithoutCandidateCannotSelectAConnection()
    {
        await using var fixture = new ViewModelFixture(
            lanNetworks: new FakeLanNetworkCatalog());

        fixture.ViewModel.IsRemoteEnabled = true;

        Assert.True(fixture.ViewModel.IsRemoteEnabled);
        Assert.False(fixture.ViewModel.IsLocalOnly);
        Assert.False(fixture.ViewModel.CanSelectLan);
        Assert.False(fixture.ViewModel.CanSelectVpn);
        Assert.Contains("見つかりません", fixture.ViewModel.RemoteConnectionSummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Tunnel, true)]
    [InlineData(NetworkInterfaceType.Ppp, true)]
    [InlineData((NetworkInterfaceType)53, true)]
    [InlineData(NetworkInterfaceType.Ethernet, false)]
    [InlineData(NetworkInterfaceType.Wireless80211, false)]
    public void VpnInterfaceClassificationDoesNotDependOnDisplayName(
        NetworkInterfaceType interfaceType,
        bool expected) =>
        Assert.Equal(expected, NetworkShareCandidateRules.IsVpnInterfaceType(interfaceType));

    [Fact]
    public async Task LanStartRequiresConfirmationAndUsesSelectedNetwork()
    {
        var first = new LanNetworkCandidate("first", "Wi-Fi", IPAddress.Parse("192.168.1.20"));
        var second = new LanNetworkCandidate("second", "Ethernet", IPAddress.Parse("10.0.0.20"));
        var dialogs = new FakeDialogs { ConfirmLan = false };
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(
            picker: picker,
            dialogs: dialogs,
            lanNetworks: new FakeLanNetworkCatalog(first, second));
        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.IsLanEnabled = true;
        fixture.ViewModel.SelectedLanNetwork = second;

        await fixture.ViewModel.StartCommand.ExecuteAsync();

        Assert.False(fixture.ViewModel.IsRunning);
        Assert.Null(fixture.Server.LastOptions);
        Assert.Equal(second.Address, dialogs.LastConfirmation?.Address);
        Assert.Contains("docs", Assert.Single(dialogs.LastConfirmation!.Shares).Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LanSafetyEventStopsSharingWithoutHidingReason()
    {
        var safety = new FakeLanSafetyMonitor();
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(picker: picker, lanSafetyMonitor: safety);
        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.IsLanEnabled = true;
        await fixture.ViewModel.StartCommand.ExecuteAsync();

        safety.Raise("network changed");
        await Task.Delay(50);

        Assert.False(fixture.ViewModel.IsRunning);
        Assert.Equal("安全のため停止", fixture.ViewModel.ServerStateText);
        Assert.Contains("network changed", fixture.ViewModel.StatusText, StringComparison.Ordinal);
        Assert.Null(safety.ArmedCandidate);
    }

    [Fact]
    public async Task DiagnosticNotificationDoesNotReplaceLanPublishingState()
    {
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        var resolver = new FakePublicIpResolver(IPAddress.Parse("203.0.113.42"));
        await using var fixture = new ViewModelFixture(picker: picker, publicIpResolver: resolver);
        fixture.ViewModel.AddShareCommand.Execute(null);
        fixture.ViewModel.IsLanEnabled = true;
        await fixture.ViewModel.StartCommand.ExecuteAsync();
        var publishingState = fixture.ViewModel.ServerStateText;

        await fixture.ViewModel.CheckGlobalIpv4Command.ExecuteAsync();

        Assert.Equal(publishingState, fixture.ViewModel.ServerStateText);
        Assert.Contains("LANへ公開中", fixture.ViewModel.ServerStateText, StringComparison.Ordinal);
        Assert.Contains("グローバルIPv4", fixture.ViewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalIpv4IsResolvedOnlyOnCommandAndCanBeCopied()
    {
        var resolver = new FakePublicIpResolver(IPAddress.Parse("203.0.113.42"));
        var clipboard = new FakeClipboard();
        await using var fixture = new ViewModelFixture(clipboard: clipboard, publicIpResolver: resolver);

        Assert.Equal(0, resolver.CallCount);
        Assert.False(fixture.ViewModel.CopyGlobalIpv4Command.CanExecute(null));

        await fixture.ViewModel.CheckGlobalIpv4Command.ExecuteAsync();

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("203.0.113.42", fixture.ViewModel.GlobalIpv4);
        Assert.True(fixture.ViewModel.CopyGlobalIpv4Command.CanExecute(null));
        Assert.Contains("接続可否は未確認", fixture.ViewModel.StatusText, StringComparison.Ordinal);

        fixture.ViewModel.CopyGlobalIpv4Command.Execute(null);
        Assert.Equal("203.0.113.42", clipboard.Text);
    }

    [Fact]
    public async Task GlobalIpv4FailureDoesNotAffectRunningServer()
    {
        var resolver = new FakePublicIpResolver(new HttpRequestException("offline"));
        var picker = new FakeFolderPicker(Path.Combine(Path.GetTempPath(), "docs"));
        await using var fixture = new ViewModelFixture(picker: picker, publicIpResolver: resolver);
        fixture.ViewModel.AddShareCommand.Execute(null);
        await fixture.ViewModel.StartCommand.ExecuteAsync();

        await fixture.ViewModel.CheckGlobalIpv4Command.ExecuteAsync();

        Assert.True(fixture.ViewModel.IsRunning);
        Assert.Equal("取得できませんでした", fixture.ViewModel.GlobalIpv4);
        Assert.Contains("offline", fixture.ViewModel.StatusText, StringComparison.Ordinal);
    }

    private sealed class ViewModelFixture : IAsyncDisposable
    {
        public ViewModelFixture(
            FakeServerController? server = null,
            FakeFolderPicker? picker = null,
            FakeClipboard? clipboard = null,
            ISettingsStore? settings = null,
            ITransferLogWriter? transferLog = null,
            ILanServerProfileFactory? lanProfiles = null,
            IPublicIpResolver? publicIpResolver = null,
            FakeDialogs? dialogs = null,
            ILanNetworkCatalog? lanNetworks = null,
            ILanSessionSafetyMonitor? lanSafetyMonitor = null,
            IOriginPortAllocator? originPortAllocator = null,
            string? applicationDirectory = null)
        {
            Server = server ?? new FakeServerController();
            ViewModel = new MainWindowViewModel(
                Server,
                picker ?? new FakeFolderPicker(),
                clipboard ?? new FakeClipboard(),
                dialogs ?? new FakeDialogs(),
                new FakeTheme(),
                settings,
                transferLog,
                lanProfiles ?? new FakeLanProfileFactory(),
                publicIpResolver,
                lanNetworks ?? new FakeLanNetworkCatalog(
                    new LanNetworkCandidate("test", "Test LAN", IPAddress.Parse("192.168.1.20"))),
                lanSafetyMonitor ?? new FakeLanSafetyMonitor(),
                originPortAllocator: originPortAllocator,
                applicationDirectory: applicationDirectory);
        }

        public FakeServerController Server { get; }

        public MainWindowViewModel ViewModel { get; }

        public async ValueTask DisposeAsync() => await ViewModel.ShutdownAsync();
    }

    private sealed class FakeFolderPicker(params string[] paths) : IFolderPickerService
    {
        private readonly Queue<string> _paths = new(paths);

        public string? PickFolder() => _paths.TryDequeue(out var path) ? path : null;
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public Exception? Exception { get; init; }

        public string? Text { get; private set; }

        public void SetText(string value)
        {
            if (Exception is not null)
            {
                throw Exception;
            }


            Text = value;
        }
    }

    private sealed class FakePublicIpResolver : IPublicIpResolver
    {
        private readonly IPAddress? _address;
        private readonly Exception? _exception;

        public FakePublicIpResolver(IPAddress address) => _address = address;

        public FakePublicIpResolver(Exception exception) => _exception = exception;

        public int CallCount { get; private set; }

        public Task<IPAddress> ResolveIpv4Async(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _exception is null
                ? Task.FromResult(_address!)
                : Task.FromException<IPAddress>(_exception);
        }
    }

    private sealed class FakeDialogs : IDialogService
    {
        public bool ConfirmLan { get; init; } = true;

        public LanStartConfirmation? LastConfirmation { get; private set; }

        public int WebsiteConfirmationCount { get; private set; }

        public void ShowSettings(MainWindowViewModel viewModel) { }

        public void ShowAbout() { }

        public bool ConfirmLanStart(LanStartConfirmation confirmation)
        {
            LastConfirmation = confirmation;
            return ConfirmLan;
        }

        public bool ConfirmWebsiteStart(WebsiteStartConfirmation confirmation)
        {
            WebsiteConfirmationCount++;
            return true;
        }
    }

    private sealed class FakeTheme : IThemeService
    {
        public bool IsClassic { get; private set; }

        public void SetClassic(bool isClassic) => IsClassic = isClassic;

        public void Toggle() => SetClassic(!IsClassic);
    }

    private sealed class FakeSettingsStore(SettingsLoadResult result, List<string>? callOrder = null) : ISettingsStore
    {
        public ApplicationSettings? Saved { get; private set; }

        public SettingsLoadResult Load() => result;

        public Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
        {
            callOrder?.Add("save");
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTransferLogWriter : ITransferLogWriter
    {
        public List<TransferRecord> Records { get; } = [];

        public bool IsDisposed { get; private set; }

        public Exception? LastError => null;

        public bool TryWrite(TransferRecord record)
        {
            Records.Add(record);
            return true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeLanProfileFactory : ILanServerProfileFactory
    {
        public string AccessCode { get; private set; } = string.Empty;

        public LanServerProfile Create(IPAddress address)
        {
            var security = LanSecurityMaterial.Create(address);
            AccessCode = security.AccessCode;
            return new LanServerProfile(address, security);
        }
    }

    private sealed class FakeLanNetworkCatalog(params LanNetworkCandidate[] candidates) : ILanNetworkCatalog
    {
        public IReadOnlyList<LanNetworkCandidate> GetCandidates() => candidates;
    }

    private sealed class FakeLanSafetyMonitor : ILanSessionSafetyMonitor
    {
        public event EventHandler<string>? StopRequired;

        public LanNetworkCandidate? ArmedCandidate { get; private set; }

        public void Arm(LanNetworkCandidate candidate) => ArmedCandidate = candidate;

        public void Disarm() => ArmedCandidate = null;

        public void Dispose() => Disarm();

        public void Raise(string reason)
        {
            if (ArmedCandidate is null)
            {
                return;
            }

            ArmedCandidate = null;
            StopRequired?.Invoke(this, reason);
        }
    }

    private sealed class FakeOriginPortAllocator(params int[] ports) : IOriginPortAllocator
    {
        private readonly Queue<int> _ports = new(ports);

        public int AllocateAndRetire() => _ports.Dequeue();
    }

    private sealed class FakeServerController : IServerController
    {
        public bool IsRunning { get; private set; }

        public IReadOnlyList<Uri> BaseAddresses { get; private set; } = [];

        public Exception? StartException { get; init; }

        public bool OmitBaseAddress { get; init; }

        public int StopCallCount { get; private set; }

        public ServerOptions? LastOptions { get; private set; }

        public List<string>? CallOrder { get; init; }

        public event EventHandler? StateChanged;

        public event EventHandler<TransferRecord>? TransferCompleted;

        public Task StartAsync(ServerOptions options, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            if (StartException is not null)
            {
                return Task.FromException(StartException);
            }

            IsRunning = true;
            BaseAddresses = OmitBaseAddress
                ? []
                : options.LanSecurity is null
                    ? [new Uri($"http://{options.ListenAddress}:{options.Port}/")]
                    :
                    [
                        new Uri($"http://127.0.0.1:{options.Port}/"),
                        new Uri($"https://{options.ListenAddress}:{options.Port}/"),
                    ];
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            CallOrder?.Add("stop");
            StopCallCount++;
            IsRunning = false;
            BaseAddresses = [];
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            CallOrder?.Add("dispose-server");
            return ValueTask.CompletedTask;
        }

        public void RaiseTransfer(int index) => TransferCompleted?.Invoke(
            this,
            new TransferRecord(DateTimeOffset.UtcNow, "GET", $"/{index}", 200, index, TimeSpan.Zero));
    }
}
