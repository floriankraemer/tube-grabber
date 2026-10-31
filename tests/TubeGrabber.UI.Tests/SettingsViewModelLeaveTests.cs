// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SettingsViewModelLeaveTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _root;
    private readonly ProfileStore _profiles;
    private readonly AppConfigStore _appConfig;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelLeaveTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _profiles = new ProfileStore(_root);
        _appConfig = new AppConfigStore(_root, _profiles);
        var coordinator = new SettingsCoordinator(
            _appConfig,
            _profiles,
            new YtDlpCommandBuilder(new YtDlpOptionCatalog(), new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
        _viewModel = ViewModelTestHelpers.CreateSettingsViewModel(coordinator, _profiles, _root);
    }

    private async Task<(bool Left, int Asked)> LeaveAsync(UnsavedChangesChoice choice)
    {
        var asked = 0;
        var left = await _viewModel.ConfirmLeaveAsync(() =>
        {
            asked++;
            return Task.FromResult(choice);
        });
        return (left, asked);
    }

    [Fact]
    public async Task CleanPage_LeavesWithoutAsking()
    {
        await _viewModel.LoadAsync();

        var (left, asked) = await LeaveAsync(UnsavedChangesChoice.Cancel);

        Assert.True(left);
        Assert.Equal(0, asked);
        Assert.False(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task DirtyProfile_CountsAsUnsaved()
    {
        await _viewModel.LoadAsync();
        _viewModel.Profiles.OpenProfile("default");
        _viewModel.Profiles.ExtraArgs = "--no-mtime";

        Assert.True(_viewModel.HasUnsavedChanges);
        Assert.Equal("Unsaved changes in 1 profile", _viewModel.UnsavedSummary);
        Assert.False(_viewModel.ShowStatusMessage);
    }

    [Fact]
    public async Task OtherSettings_CountAsUnsaved_AndRevertingClearsIt()
    {
        await _viewModel.LoadAsync();
        var original = _viewModel.MaxConcurrentDownloads;

        _viewModel.MaxConcurrentDownloads = original + 1;
        Assert.True(_viewModel.HasUnsavedChanges);
        Assert.Equal("Unsaved changes", _viewModel.UnsavedSummary);

        _viewModel.MaxConcurrentDownloads = original;
        Assert.False(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task Cancel_StaysOnTheSettingsPage_AndKeepsTheEdits()
    {
        await _viewModel.LoadAsync();
        _viewModel.Profiles.OpenProfile("default");
        _viewModel.Profiles.ExtraArgs = "--no-mtime";

        var (left, asked) = await LeaveAsync(UnsavedChangesChoice.Cancel);

        Assert.False(left);
        Assert.Equal(1, asked);
        Assert.True(_viewModel.HasUnsavedChanges);
        Assert.Equal("--no-mtime", _viewModel.Profiles.ExtraArgs);
    }

    [Fact]
    public async Task Discard_DropsTheEditsAndLeaves()
    {
        await _viewModel.LoadAsync();
        _viewModel.Profiles.OpenProfile("default");
        _viewModel.Profiles.ExtraArgs = "--no-mtime";
        _viewModel.MaxConcurrentDownloads = 7;

        var (left, _) = await LeaveAsync(UnsavedChangesChoice.Discard);

        Assert.True(left);
        Assert.False(_viewModel.HasUnsavedChanges);
        Assert.Equal(string.Empty, _viewModel.Profiles.ExtraArgs);
        Assert.Equal("", (await _profiles.GetAsync("default", Ct))!.ExtraArgs);
    }

    [Fact]
    public async Task Save_WritesTheEditsAndLeaves()
    {
        await _viewModel.LoadAsync();
        _viewModel.DownloadFolder = _root;
        _viewModel.Profiles.OpenProfile("default");
        _viewModel.Profiles.ExtraArgs = "--no-mtime";

        var (left, _) = await LeaveAsync(UnsavedChangesChoice.Save);

        Assert.True(left);
        Assert.False(_viewModel.HasUnsavedChanges);
        Assert.Equal("--no-mtime", (await _profiles.GetAsync("default", Ct))!.ExtraArgs);
    }

    [Fact]
    public async Task Save_WithAValidationError_StaysOnTheSettingsPage()
    {
        await _viewModel.LoadAsync();
        _viewModel.MaxConcurrentDownloads = 99;

        var (left, _) = await LeaveAsync(UnsavedChangesChoice.Save);

        Assert.False(left);
        Assert.NotNull(_viewModel.ValidationError);
        Assert.False(_viewModel.ShowUnsavedSummary);
    }

    [Fact]
    public async Task Save_DoesNotChangeTheActiveProfileOfTheOtherPages()
    {
        await _viewModel.LoadAsync();
        var before = (await _appConfig.LoadAsync(Ct)).ActiveProfileId;
        _viewModel.DownloadFolder = _root;
        var other = _viewModel.Profiles.Cards.First(c => c.Id != before).Id;
        _viewModel.Profiles.OpenProfile(other);
        _viewModel.Profiles.ExtraArgs = "--no-mtime";

        Assert.True(await _viewModel.SaveAsync());

        Assert.Equal(before, (await _appConfig.LoadAsync(Ct)).ActiveProfileId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
