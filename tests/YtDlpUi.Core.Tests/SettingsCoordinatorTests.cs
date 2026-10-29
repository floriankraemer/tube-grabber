// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class SettingsCoordinatorTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsCoordinator _coordinator;

    public SettingsCoordinatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
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
        var result = await _coordinator.TestFfmpegAsync(new AppConfiguration());
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
        var (config, profiles) = await _coordinator.LoadAsync();
        config.RunInBackground = false;
        config.StartAtLogin = true;
        config.SubscriptionCheckIntervalMinutes = 90;

        await _coordinator.SaveAsync(config, profiles[0]);
        var (reloaded, _) = await _coordinator.LoadAsync();

        Assert.False(reloaded.RunInBackground);
        Assert.True(reloaded.StartAtLogin);
        Assert.Equal(90, reloaded.SubscriptionCheckIntervalMinutes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
