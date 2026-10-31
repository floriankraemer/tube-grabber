// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SettingsViewModelExtendedTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsViewModel _viewModel;
    private readonly ProfileStore _profiles;
    private readonly AppConfigStore _appConfig;

    public SettingsViewModelExtendedTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _profiles = new ProfileStore(_root);
        _appConfig = new AppConfigStore(_root, _profiles);
        var catalog = new YtDlpOptionCatalog();
        var coordinator = new SettingsCoordinator(
            _appConfig,
            _profiles,
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
        _viewModel = ViewModelTestHelpers.CreateSettingsViewModel(coordinator, _profiles, _root);
    }

    [Fact]
    public async Task DiscardingByReload_DropsUnsavedEdits()
    {
        await _viewModel.LoadAsync();
        _viewModel.Profiles.OpenProfile(AppPaths.DefaultProfileId);
        _viewModel.Profiles.ExtraArgs = "--no-mtime";
        _viewModel.MaxConcurrentDownloads = 5;

        await _viewModel.LoadAsync();

        Assert.Equal(string.Empty, _viewModel.Profiles.ExtraArgs);
        Assert.False(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void ApplyTestResult_SetsPerToolStatus()
    {
        _viewModel.ApplyTestResult("ffmpeg", "7.1");
        _viewModel.ApplyTestResult("yt-dlp", "yt-dlp not found");

        Assert.False(_viewModel.FfmpegStatus.IsError);
        Assert.Equal("7.1", _viewModel.FfmpegStatus.Text);
        Assert.True(_viewModel.YtDlpStatus.IsError);
        Assert.False(_viewModel.JsRuntimeStatus.HasText);
    }

    [Fact]
    public async Task TestYtDlpAsync_ReturnsErrorWhenMissing()
    {
        await _viewModel.LoadAsync();
        var version = await _viewModel.TestYtDlpAsync();
        Assert.Contains("not found", version ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestFfmpegAsync_ReturnsErrorWhenMissing()
    {
        await _viewModel.LoadAsync();
        var version = await _viewModel.TestFfmpegAsync();
        Assert.Contains("not found", version ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyTestResult_SetsStatusMessageOnSuccess()
    {
        _viewModel.ApplyTestResult("yt-dlp", "2024.12.23");
        Assert.Contains("2024.12.23", _viewModel.StatusMessage ?? string.Empty);
        Assert.Null(_viewModel.ValidationError);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
