// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SettingsViewModelValidationTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelValidationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
        var catalog = new YtDlpOptionCatalog();
        var coordinator = new SettingsCoordinator(
            appConfig,
            profiles,
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
        _viewModel = ViewModelTestHelpers.CreateSettingsViewModel(coordinator, profiles, _root);
    }

    [Fact]
    public async Task SaveAsync_InvalidConcurrency_Fails()
    {
        await _viewModel.LoadAsync();
        _viewModel.MaxConcurrentDownloads = 99;
        var saved = await _viewModel.SaveAsync();
        Assert.False(saved);
        Assert.NotNull(_viewModel.ValidationError);
    }

    [Fact]
    public async Task SaveAsync_OutOfRangeCheckInterval_FailsWithError()
    {
        await _viewModel.LoadAsync();
        _viewModel.SubscriptionCheckIntervalMinutes = 5;
        var saved = await _viewModel.SaveAsync();
        Assert.False(saved);
        Assert.Contains("Subscription check interval", _viewModel.ValidationError);
    }

    [Fact]
    public async Task SaveAsync_InvalidExtraArgs_NamesTheProfileAndPointsAtIt()
    {
        await _viewModel.LoadAsync();
        _viewModel.Profiles.OpenProfile("hq-video");
        _viewModel.Profiles.ExtraArgs = "--x \"unterminated";

        var saved = await _viewModel.SaveAsync();

        Assert.False(saved);
        Assert.StartsWith("Download HQ Video: ", _viewModel.ValidationError);
        Assert.Equal("hq-video", _viewModel.ValidationProfileId);
        _viewModel.Profiles.CloseProfile();
        _viewModel.OpenFailingProfile();
        Assert.Equal("hq-video", _viewModel.Profiles.EditingProfile?.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
