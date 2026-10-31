// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SettingsViewModelBackgroundTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileStore _profiles;
    private readonly AppConfigStore _appConfig;
    private readonly ILoginItemService _loginItem;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelBackgroundTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _profiles = new ProfileStore(_root);
        _appConfig = new AppConfigStore(_root, _profiles);
        _loginItem = Substitute.For<ILoginItemService>();
        _viewModel = ViewModelTestHelpers.CreateSettingsViewModel(
            CreateCoordinator(),
            _profiles,
            _root,
            loginItemService: _loginItem);
    }

    [Fact]
    public async Task SaveAsync_PersistsBackgroundSettings()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        _viewModel.RunInBackground = false;
        _viewModel.SubscriptionCheckIntervalMinutes = 30;

        Assert.True(await _viewModel.SaveAsync());

        var config = await _appConfig.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(config.RunInBackground);
        Assert.Equal(30, config.SubscriptionCheckIntervalMinutes);
    }

    [Fact]
    public async Task LoadAsync_ReadsBackgroundSettingsFromConfig()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        _viewModel.StartAtLogin = true;
        _viewModel.SubscriptionCheckIntervalMinutes = 720;
        await _viewModel.SaveAsync();

        var reloaded = ViewModelTestHelpers.CreateSettingsViewModel(
            CreateCoordinator(),
            _profiles,
            _root,
            loginItemService: _loginItem);
        await reloaded.LoadAsync();

        Assert.True(reloaded.StartAtLogin);
        Assert.True(reloaded.RunInBackground);
        Assert.Equal(720, reloaded.SubscriptionCheckIntervalMinutes);
    }

    [Fact]
    public async Task SaveAsync_StartAtLoginChanged_RegistersLoginItem()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        _viewModel.StartAtLogin = true;

        Assert.True(await _viewModel.SaveAsync());

        _loginItem.Received(1).SetEnabled(true);
    }

    [Fact]
    public async Task SaveAsync_StartAtLoginUnchanged_DoesNotTouchLoginItem()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;

        Assert.True(await _viewModel.SaveAsync());

        _loginItem.DidNotReceive().SetEnabled(Arg.Any<bool>());
    }

    [Fact]
    public async Task SaveAsync_LoginItemFails_ReportsErrorAndKeepsWindowOpen()
    {
        _loginItem.When(x => x.SetEnabled(true)).Do(_ => throw new IOException("autostart folder is read-only"));
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        _viewModel.StartAtLogin = true;

        var saved = await _viewModel.SaveAsync();

        Assert.False(saved);
        Assert.Contains("autostart folder is read-only", _viewModel.ValidationError ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_IntervalOutOfRange_FailsValidation()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        _viewModel.SubscriptionCheckIntervalMinutes = 5;

        Assert.False(await _viewModel.SaveAsync());

        Assert.NotNull(_viewModel.ValidationError);
        _loginItem.DidNotReceive().SetEnabled(Arg.Any<bool>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private SettingsCoordinator CreateCoordinator() =>
        new(
            _appConfig,
            _profiles,
            new YtDlpCommandBuilder(new YtDlpOptionCatalog(), new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
}
