// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class SettingsCoordinatorTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsCoordinator _coordinator;
    private readonly ProfileStore _profileStore;
    private readonly AppConfigStore _appConfig;

    public SettingsCoordinatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        var profiles = _profileStore = new ProfileStore(_root);
        var appConfig = _appConfig = new AppConfigStore(_root, profiles);
        var catalog = new YtDlpOptionCatalog();
        _coordinator = new SettingsCoordinator(
            appConfig,
            profiles,
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            new AppSettingsValidator(new ExtraArgsTokenizer(), new DownloadFolderService()),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
    }

    [Fact]
    public async Task TestFfmpegAsync_ReturnsNotFoundWhenMissing()
    {
        var result = await _coordinator.TestFfmpegAsync(new AppConfiguration(), TestContext.Current.CancellationToken);
        Assert.Contains("not found", result ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCliPreview_IncludesJsRuntimesWhenConfigured()
    {
        Directory.CreateDirectory(_root);
        var denoPath = Path.Combine(_root, "deno");
        File.WriteAllText(denoPath, string.Empty);

        var config = new AppConfiguration
        {
            JsRuntimeEngine = Core.Constants.JsRuntimeEngines.Deno,
            JsRuntimePath = denoPath,
        };
        var profile = new DownloadProfile { Id = "p", Name = "P" };

        var preview = _coordinator.BuildCliPreview(config, profile);
        Assert.Contains("--js-runtimes", preview, StringComparison.Ordinal);
        Assert.Contains("deno:", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveYtDlpPath_UsesConfiguredPathWhenFileExists()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "custom-yt-dlp");
        File.WriteAllText(path, string.Empty);

        var config = new AppConfiguration { YtDlpPath = path };
        var resolved = _coordinator.ResolveYtDlpPath(config);

        Assert.Equal(Path.GetFullPath(path), resolved);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsBackgroundSettings()
    {
        var (config, profiles) = await _coordinator.LoadAsync(TestContext.Current.CancellationToken);
        config.RunInBackground = false;
        config.StartAtLogin = true;
        config.SubscriptionCheckIntervalMinutes = 90;

        await _coordinator.SaveAsync(config, [profiles[0]], TestContext.Current.CancellationToken);
        var (reloaded, _) = await _coordinator.LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(reloaded.RunInBackground);
        Assert.True(reloaded.StartAtLogin);
        Assert.Equal(90, reloaded.SubscriptionCheckIntervalMinutes);
    }

    [Fact]
    public async Task SaveAsync_KeepsActiveProfileChosenElsewhere()
    {
        var (config, profiles) = await _coordinator.LoadAsync(TestContext.Current.CancellationToken);
        var stale = config.ActiveProfileId;
        var other = profiles.First(p => p.Id != stale);
        var latest = await _appConfig.LoadAsync(TestContext.Current.CancellationToken);
        latest.ActiveProfileId = other.Id;
        latest.QueueColumnWidths["Title"] = 321;
        await _appConfig.SaveAsync(latest, TestContext.Current.CancellationToken);

        await _coordinator.SaveAsync(config, [profiles[0]], TestContext.Current.CancellationToken);

        var (reloaded, _) = await _coordinator.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(other.Id, reloaded.ActiveProfileId);
        Assert.Equal(321, reloaded.QueueColumnWidths["Title"]);
    }

    [Fact]
    public async Task DeleteProfileAsync_RefusesBuiltInProfile()
    {
        await _coordinator.LoadAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _coordinator.DeleteProfileAsync(Core.Constants.AppPaths.HqVideoProfileId, TestContext.Current.CancellationToken));
        Assert.NotNull(await _profileStore.GetAsync(Core.Constants.AppPaths.HqVideoProfileId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteProfileAsync_ResetsActiveProfileWhenItWasTheDeletedOne()
    {
        await _coordinator.LoadAsync(TestContext.Current.CancellationToken);
        await _profileStore.SaveAsync(new DownloadProfile { Id = "mine", Name = "Mine" }, TestContext.Current.CancellationToken);
        var config = await _appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.ActiveProfileId = "mine";
        await _appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        await _coordinator.DeleteProfileAsync("mine", TestContext.Current.CancellationToken);

        Assert.Null(await _profileStore.GetAsync("mine", TestContext.Current.CancellationToken));
        Assert.Equal(Core.Constants.AppPaths.DefaultProfileId, (await _appConfig.LoadAsync(TestContext.Current.CancellationToken)).ActiveProfileId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
