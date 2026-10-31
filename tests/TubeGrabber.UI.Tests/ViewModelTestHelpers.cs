// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

internal static class ViewModelTestHelpers
{
    public static AppSettingsValidator CreateValidator() =>
        new(new ExtraArgsTokenizer(), new DownloadFolderService());

    public static BinaryInstallService CreateBinaryInstallService(
        IAppConfigStore? appConfig = null,
        IBinaryLocator? binaryLocator = null) =>
        new(
            appConfig ?? Substitute.For<IAppConfigStore>(),
            binaryLocator ?? new BinaryLocator(Path.GetTempPath()));

    public static DownloadEnqueueCoordinator CreateEnqueueCoordinator(
        IDownloadQueueService? queue = null,
        IAppConfigStore? appConfig = null,
        IProfileStore? profileStore = null,
        IBinaryLocator? binaryLocator = null) =>
        new(
            queue ?? Substitute.For<IDownloadQueueService>(),
            appConfig ?? Substitute.For<IAppConfigStore>(),
            profileStore ?? Substitute.For<IProfileStore>(),
            new YouTubeUrlNormalizer(),
            binaryLocator ?? new BinaryLocator(Path.GetTempPath()),
            new DownloadFolderService());

    public static MainWindowViewModel CreateMainViewModel(
        IDownloadQueueService? queue = null,
        IAppConfigStore? appConfig = null,
        IProfileStore? profileStore = null,
        DownloadEnqueueCoordinator? enqueueCoordinator = null,
        DownloadFolderService? downloadFolderService = null,
        IFileSystemLauncher? fileSystemLauncher = null)
    {
        if (queue is null)
        {
            queue = Substitute.For<IDownloadQueueService>();
            queue.Jobs.Returns(Array.Empty<DownloadJob>());
        }

        appConfig ??= Substitute.For<IAppConfigStore>();
        profileStore ??= Substitute.For<IProfileStore>();
        downloadFolderService ??= new DownloadFolderService();

        return new MainWindowViewModel(
            queue,
            appConfig,
            profileStore,
            enqueueCoordinator ?? CreateEnqueueCoordinator(queue, appConfig, profileStore),
            downloadFolderService,
            new YouTubeUrlNormalizer(),
            fileSystemLauncher ?? Substitute.For<IFileSystemLauncher>());
    }

    public static SettingsViewModel CreateSettingsViewModel(
        SettingsCoordinator coordinator,
        IProfileStore profileStore,
        string configRoot,
        IBinaryInstaller? ytDlpInstaller = null,
        IBinaryInstaller? ffmpegInstaller = null,
        IBinaryInstaller? denoInstaller = null,
        BinaryInstallService? binaryInstallService = null,
        IFileSystemLauncher? fileSystemLauncher = null,
        ILoginItemService? loginItemService = null,
        ISubscriptionMonitor? subscriptionMonitor = null)
    {
        var appConfig = new AppConfigStore(configRoot, profileStore);
        return new SettingsViewModel(
            coordinator,
            CreateProfilesViewModel(coordinator, profileStore, subscriptionMonitor),
            new DownloadFolderService(),
            ytDlpInstaller ?? Substitute.For<IBinaryInstaller>(),
            ffmpegInstaller ?? Substitute.For<IBinaryInstaller>(),
            denoInstaller ?? Substitute.For<IBinaryInstaller>(),
            binaryInstallService ?? CreateBinaryInstallService(appConfig, new BinaryLocator(configRoot)),
            fileSystemLauncher ?? Substitute.For<IFileSystemLauncher>(),
            loginItemService ?? Substitute.For<ILoginItemService>());
    }

    public static ProfilesViewModel CreateProfilesViewModel(
        SettingsCoordinator coordinator,
        IProfileStore profileStore,
        ISubscriptionMonitor? subscriptionMonitor = null)
    {
        if (subscriptionMonitor is null)
        {
            subscriptionMonitor = Substitute.For<ISubscriptionMonitor>();
            subscriptionMonitor.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<ChannelSubscription>());
        }

        return new ProfilesViewModel(coordinator, new YtDlpOptionCatalog(), profileStore, subscriptionMonitor);
    }
}
