// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Tests;

public sealed class SettingsViewModelDeleteTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileStore _profiles;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelDeleteTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        _profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, _profiles);
        var catalog = new YtDlpOptionCatalog();
        var coordinator = new SettingsCoordinator(
            appConfig,
            _profiles,
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
        _viewModel = ViewModelTestHelpers.CreateSettingsViewModel(coordinator, _profiles, _root);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesSecondaryProfile()
    {
        await _profiles.SaveAsync(new DownloadProfile { Id = "default", Name = "Default" }, TestContext.Current.CancellationToken);
        await _profiles.SaveAsync(new DownloadProfile { Id = "extra", Name = "Extra" }, TestContext.Current.CancellationToken);
        await _viewModel.LoadAsync();
        await _viewModel.SelectProfileAsync(_viewModel.Profiles.First(p => p.Id == "extra"));
        await _viewModel.DeleteProfileAsync();
        Assert.DoesNotContain(_viewModel.Profiles, p => p.Id == "extra");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
