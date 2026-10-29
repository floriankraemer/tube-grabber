// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class AppConfigStoreTests : IDisposable
{
    private readonly string _root;
    private readonly AppConfigStore _store;

    public AppConfigStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(_root);
        _store = new AppConfigStore(_root, profiles);
    }

    [Fact]
    public async Task EnsureBootstrap_CreatesDefaultProfileAndConfig()
    {
        await _store.EnsureBootstrapAsync();
        var config = await _store.LoadAsync();
        Assert.Equal(AppPaths.DefaultProfileId, config.ActiveProfileId);
        Assert.True(File.Exists(Path.Combine(_root, AppPaths.AppConfigFileName)));
        Assert.True(File.Exists(Path.Combine(_root, AppPaths.ProfilesFolderName, $"{AppPaths.DefaultProfileId}.json")));
    }

    [Fact]
    public async Task ConcurrentFirstRunLoadsAndSaves_DoNotCollide()
    {
        var tasks = Enumerable.Range(0, 32)
            .Select(i => Task.Run(async () =>
            {
                var config = await _store.LoadAsync();
                if (i % 4 == 0)
                    await _store.SaveAsync(config);
            }));

        await Task.WhenAll(tasks);

        var loaded = await _store.LoadAsync();
        Assert.Equal(AppPaths.DefaultProfileId, loaded.ActiveProfileId);
    }

    [Fact]
    public async Task EnsureBootstrap_CreatesAllBuiltInProfiles()
    {
        await _store.EnsureBootstrapAsync();

        Assert.True(File.Exists(Path.Combine(_root, AppPaths.ProfilesFolderName, $"{AppPaths.AudioMp3ProfileId}.json")));
        Assert.True(File.Exists(Path.Combine(_root, AppPaths.ProfilesFolderName, $"{AppPaths.HqVideoProfileId}.json")));
    }

    [Fact]
    public async Task EnsureBootstrap_SyncsMissingOptionsIntoExistingBuiltInProfile()
    {
        var profiles = new ProfileStore(_root);
        await profiles.SaveAsync(new DownloadProfile
        {
            Id = AppPaths.AudioMp3ProfileId,
            Name = "Download Audio as mp3",
            Options = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["-f"] = "bestaudio",
                ["--no-part"] = true,
            },
        });

        await _store.EnsureBootstrapAsync();

        var loaded = await profiles.GetAsync(AppPaths.AudioMp3ProfileId);
        Assert.True(loaded!.Options.ContainsKey("--force-overwrites"));
        Assert.True(loaded.Options.ContainsKey("-x"));
        Assert.Equal("mp3", loaded.Options["--audio-format"]?.ToString());
    }

    [Fact]
    public async Task EnsureBootstrap_AddsMissingBuiltInProfilesWithoutOverwritingExisting()
    {
        var profiles = new ProfileStore(_root);
        var customDefault = BuiltInProfiles.CreateDefault();
        customDefault.Options["-f"] = "custom-format";
        await profiles.SaveAsync(customDefault);

        await _store.EnsureBootstrapAsync();

        var loadedDefault = await profiles.GetAsync(AppPaths.DefaultProfileId);
        Assert.Equal("custom-format", loadedDefault!.Options["-f"]?.ToString());
        Assert.True(File.Exists(Path.Combine(_root, AppPaths.ProfilesFolderName, $"{AppPaths.AudioMp3ProfileId}.json")));
    }

    [Fact]
    public async Task SaveAsync_PersistsChanges()
    {
        var config = await _store.LoadAsync();
        config.MaxConcurrentDownloads = 4;
        await _store.SaveAsync(config);
        var loaded = await _store.LoadAsync();
        Assert.Equal(4, loaded.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task SaveAsync_PersistsQueueColumnWidths()
    {
        var config = await _store.LoadAsync();
        config.QueueColumnWidths["title"] = 350;
        config.QueueColumnWidths["url"] = 420;
        await _store.SaveAsync(config);

        var loaded = await _store.LoadAsync();
        Assert.Equal(350, loaded.QueueColumnWidths["title"]);
        Assert.Equal(420, loaded.QueueColumnWidths["url"]);
    }

    [Fact]
    public async Task SaveAsync_PersistsThemePreference()
    {
        var config = await _store.LoadAsync();
        config.ThemePreference = ThemePreference.Dark;
        await _store.SaveAsync(config);

        var loaded = await _store.LoadAsync();
        Assert.Equal(ThemePreference.Dark, loaded.ThemePreference);
    }

    [Fact]
    public async Task SaveAsync_PersistsSubscriptionAndBackgroundSettings()
    {
        var config = await _store.LoadAsync();
        config.SubscriptionCheckIntervalMinutes = 240;
        config.RunInBackground = false;
        config.StartAtLogin = true;
        await _store.SaveAsync(config);

        var loaded = await _store.LoadAsync();
        Assert.Equal(240, loaded.SubscriptionCheckIntervalMinutes);
        Assert.False(loaded.RunInBackground);
        Assert.True(loaded.StartAtLogin);
    }

    [Fact]
    public async Task LoadAsync_FromV3WithoutNewFields_AppliesDefaults()
    {
        Directory.CreateDirectory(_root);
        var v3Json = """
            {
              "SchemaVersion": 3,
              "MaxConcurrentDownloads": 3,
              "ActiveProfileId": "default",
              "ThemePreference": "Dark"
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_root, AppPaths.AppConfigFileName), v3Json);

        var loaded = await _store.LoadAsync();

        Assert.Equal(SchemaVersions.AppConfig, loaded.SchemaVersion);
        Assert.Equal(3, loaded.MaxConcurrentDownloads);
        Assert.Equal(ThemePreference.Dark, loaded.ThemePreference);
        Assert.Equal(AppPaths.DefaultSubscriptionCheckIntervalMinutes, loaded.SubscriptionCheckIntervalMinutes);
        Assert.True(loaded.RunInBackground);
        Assert.False(loaded.StartAtLogin);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
