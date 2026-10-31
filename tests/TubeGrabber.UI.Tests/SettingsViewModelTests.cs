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

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileStore _profiles;
    private readonly AppConfigStore _appConfig;
    private readonly SettingsCoordinator _coordinator;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _profiles = new ProfileStore(_root);
        _appConfig = new AppConfigStore(_root, _profiles);
        var catalog = new YtDlpOptionCatalog();
        _coordinator = new SettingsCoordinator(
            _appConfig,
            _profiles,
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
        _viewModel = ViewModelTestHelpers.CreateSettingsViewModel(_coordinator, _profiles, _root);
    }

    [Fact]
    public async Task LoadAsync_ShowsTheProfileListWithoutOpeningAProfile()
    {
        await _viewModel.LoadAsync();
        Assert.True(_viewModel.Profiles.IsListMode);
        Assert.NotEmpty(_viewModel.Profiles.Cards);
        _viewModel.Profiles.OpenProfile("default");
        Assert.False(string.IsNullOrWhiteSpace(_viewModel.Profiles.CliPreview));
    }

    [Fact]
    public async Task SaveAsync_ValidProfile_Succeeds()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        var saved = await _viewModel.SaveAsync();
        Assert.True(saved);
        Assert.Null(_viewModel.ValidationError);
    }

    [Fact]
    public async Task LoadAsync_PrefillsResolvedYtDlpPath_WhenConfigEmpty()
    {
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        var ytDlpName = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
        var ytDlpPath = Path.Combine(ytDlpDir, ytDlpName);
        await File.WriteAllTextAsync(ytDlpPath, string.Empty, TestContext.Current.CancellationToken);

        await _viewModel.LoadAsync();

        Assert.Equal(Path.GetFullPath(ytDlpPath), _viewModel.YtDlpPath);
    }

    [Fact]
    public async Task InstallYtDlpAsync_PersistsPathAndUpdatesField()
    {
        var installedPath = Path.Combine(_root, "bin", "yt-dlp", OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        Directory.CreateDirectory(Path.GetDirectoryName(installedPath)!);
        await File.WriteAllTextAsync(installedPath, string.Empty, TestContext.Current.CancellationToken);

        var installer = Substitute.For<IBinaryInstaller>();
        installer.InstallAsync(Arg.Any<CancellationToken>())
            .Returns(BinaryInstallResult.Success(installedPath));

        var vm = ViewModelTestHelpers.CreateSettingsViewModel(
            _coordinator,
            _profiles,
            _root,
            ytDlpInstaller: installer,
            binaryInstallService: ViewModelTestHelpers.CreateBinaryInstallService(_appConfig, new BinaryLocator(_root)));

        await vm.LoadAsync();
        await vm.InstallYtDlpAsync();

        Assert.Equal(installedPath, vm.YtDlpPath);
        Assert.Equal("yt-dlp installed.", vm.StatusMessage);
        var config = await _appConfig.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(installedPath, config.YtDlpPath);
    }

    [Fact]
    public async Task InstallFfmpegAsync_SetsErrorOnFailure()
    {
        const string releaseUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases";
        var installer = Substitute.For<IBinaryInstaller>();
        installer.InstallAsync(Arg.Any<CancellationToken>())
            .Returns(BinaryInstallResult.Failure("network error", releaseUrl));

        var launcher = Substitute.For<IFileSystemLauncher>();
        launcher.TryOpenUrl(releaseUrl).Returns(true);

        var vm = ViewModelTestHelpers.CreateSettingsViewModel(
            _coordinator,
            _profiles,
            _root,
            ffmpegInstaller: installer,
            binaryInstallService: ViewModelTestHelpers.CreateBinaryInstallService(_appConfig, new BinaryLocator(_root)),
            fileSystemLauncher: launcher);

        await vm.LoadAsync();
        await vm.InstallFfmpegAsync();

        Assert.Equal("network error", vm.ValidationError);
        launcher.Received(1).TryOpenUrl(releaseUrl);
    }

    [Fact]
    public void OpenYtDlpReleasesPage_OpensReleaseUrl()
    {
        const string releaseUrl = "https://github.com/yt-dlp/yt-dlp/releases";
        var launcher = Substitute.For<IFileSystemLauncher>();
        launcher.TryOpenUrl(releaseUrl).Returns(true);

        var vm = ViewModelTestHelpers.CreateSettingsViewModel(
            _coordinator,
            _profiles,
            _root,
            fileSystemLauncher: launcher);

        vm.OpenYtDlpReleasesPage();

        launcher.Received(1).TryOpenUrl(releaseUrl);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
