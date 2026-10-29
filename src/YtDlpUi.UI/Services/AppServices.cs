// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Services;
using YtDlpUi.UI.Services.Notifications;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Services;

public sealed class AppServices
{
    public AppServices()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        ConfigRoot = Path.Combine(home, Core.Constants.AppPaths.ConfigFolderName);
        ProfileStore = new ProfileStore(ConfigRoot);
        AppConfigStore = new AppConfigStore(ConfigRoot, ProfileStore);
        Catalog = new YtDlpOptionCatalog();
        ExtraArgsTokenizer = new ExtraArgsTokenizer();
        CommandBuilder = new YtDlpCommandBuilder(Catalog, ExtraArgsTokenizer);
        BinaryLocator = new BinaryLocator(ConfigRoot);
        JsRuntimeLocator = new JsRuntimeLocator();
        UrlNormalizer = new YouTubeUrlNormalizer();
        ProgressParser = new YtDlpProgressParser();
        OutputPathParser = new YtDlpOutputPathParser();
        MetadataParser = new YtDlpMetadataParser();
        DownloadFolderService = new DownloadFolderService();
        Validator = new AppSettingsValidator(ExtraArgsTokenizer, DownloadFolderService);
        ReleaseSource = new GitHubBinaryReleaseSource();
        DownloadHelper = new BinaryDownloadHelper();
        ProcessRunner = new YtDlpProcessRunner();
        SearchResultParser = new YtDlpSearchResultParser();
        SearchService = new YtDlpSearchService(
            ProcessRunner,
            AppConfigStore,
            BinaryLocator,
            JsRuntimeLocator,
            SearchResultParser);
        Queue = new DownloadQueueService(
            ProcessRunner,
            CommandBuilder,
            AppConfigStore,
            ProfileStore,
            BinaryLocator,
            UrlNormalizer,
            ProgressParser,
            OutputPathParser,
            MetadataParser,
            DownloadFolderService,
            JsRuntimeLocator);
        EnqueueCoordinator = new DownloadEnqueueCoordinator(
            Queue,
            AppConfigStore,
            ProfileStore,
            UrlNormalizer,
            BinaryLocator,
            DownloadFolderService);
        HttpClient = new HttpClient();
        ThumbnailLoader = new ThumbnailLoader(HttpClient);
        BinaryInstallService = new BinaryInstallService(AppConfigStore, BinaryLocator);
        YtDlpInstaller = new YtDlpBinaryInstaller(ReleaseSource, BinaryLocator, DownloadHelper);
        FfmpegInstaller = new FfmpegBinaryInstaller(ReleaseSource, BinaryLocator, DownloadHelper);
        DenoInstaller = new DenoBinaryInstaller(ReleaseSource, BinaryLocator, DownloadHelper);
        FileSystemLauncher = new FileSystemLauncherService();
        StoragePicker = new StoragePickerService();
        LoginItemService = new LoginItemService();
        DesktopNotifier = DesktopNotifierFactory.Create();
        SubscriptionMonitor = new SubscriptionMonitor(
            new SubscriptionStore(ConfigRoot),
            new ChannelFeedService(
                new YtDlpFlatPlaylistRunner(ProcessRunner, AppConfigStore, BinaryLocator, JsRuntimeLocator),
                UrlNormalizer),
            EnqueueCoordinator,
            Queue,
            AppConfigStore,
            DesktopNotifier);
    }

    public string ConfigRoot { get; }
    public IProfileStore ProfileStore { get; }
    public IAppConfigStore AppConfigStore { get; }
    public YtDlpOptionCatalog Catalog { get; }
    public ExtraArgsTokenizer ExtraArgsTokenizer { get; }
    public YtDlpCommandBuilder CommandBuilder { get; }
    public IBinaryLocator BinaryLocator { get; }
    public IJsRuntimeLocator JsRuntimeLocator { get; }
    public YouTubeUrlNormalizer UrlNormalizer { get; }
    public YtDlpProgressParser ProgressParser { get; }
    public YtDlpOutputPathParser OutputPathParser { get; }
    public YtDlpMetadataParser MetadataParser { get; }
    public YtDlpSearchResultParser SearchResultParser { get; }
    public IYtDlpSearchService SearchService { get; }
    public AppSettingsValidator Validator { get; }
    public IBinaryReleaseSource ReleaseSource { get; }
    public BinaryDownloadHelper DownloadHelper { get; }
    public IYtDlpProcessRunner ProcessRunner { get; }
    public DownloadFolderService DownloadFolderService { get; }
    public IDownloadQueueService Queue { get; }
    public DownloadEnqueueCoordinator EnqueueCoordinator { get; }
    public HttpClient HttpClient { get; }
    public IThumbnailLoader ThumbnailLoader { get; }
    public BinaryInstallService BinaryInstallService { get; }
    public IBinaryInstaller YtDlpInstaller { get; }
    public IBinaryInstaller FfmpegInstaller { get; }
    public IBinaryInstaller DenoInstaller { get; }
    public IFileSystemLauncher FileSystemLauncher { get; }
    public StoragePickerService StoragePicker { get; }
    public ILoginItemService LoginItemService { get; }
    public IDesktopNotifier DesktopNotifier { get; }
    public SubscriptionMonitor SubscriptionMonitor { get; }

    public MainWindowViewModel CreateMainViewModel() => new(
        Queue,
        AppConfigStore,
        ProfileStore,
        EnqueueCoordinator,
        DownloadFolderService,
        UrlNormalizer,
        FileSystemLauncher);

    public SearchViewModel CreateSearchViewModel() => new(
        SearchService,
        AppConfigStore,
        ProfileStore,
        EnqueueCoordinator,
        ThumbnailLoader);

    public SettingsCoordinator CreateSettingsCoordinator() => new(
        AppConfigStore,
        ProfileStore,
        CommandBuilder,
        Validator,
        BinaryLocator,
        JsRuntimeLocator);

    public SettingsViewModel CreateSettingsViewModel() => new(
        CreateSettingsCoordinator(),
        Catalog,
        ProfileStore,
        DownloadFolderService,
        YtDlpInstaller,
        FfmpegInstaller,
        DenoInstaller,
        BinaryInstallService,
        FileSystemLauncher,
        LoginItemService);
}
