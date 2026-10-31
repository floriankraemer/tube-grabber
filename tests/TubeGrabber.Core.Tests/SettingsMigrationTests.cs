// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class SettingsMigrationTests
{
    [Fact]
    public void MigrateAppConfig_UpgradesSchemaVersion()
    {
        var config = new AppConfiguration { SchemaVersion = 0 };
        var migrated = SettingsMigration.MigrateAppConfig(config);
        Assert.Equal(SchemaVersions.AppConfig, migrated.SchemaVersion);
    }

    [Fact]
    public void MigrateAppConfig_FromV2_SetsThemePreferenceSystem()
    {
        var config = new AppConfiguration { SchemaVersion = 2 };
        var migrated = SettingsMigration.MigrateAppConfig(config);
        Assert.Equal(SchemaVersions.AppConfig, migrated.SchemaVersion);
        Assert.Equal(ThemePreference.System, migrated.ThemePreference);
    }

    [Fact]
    public void MigrateAppConfig_FromV3_SetsSubscriptionAndBackgroundDefaults()
    {
        var config = new AppConfiguration
        {
            SchemaVersion = 3,
            SubscriptionCheckIntervalMinutes = 0,
            RunInBackground = false,
            StartAtLogin = true,
        };
        var migrated = SettingsMigration.MigrateAppConfig(config);
        Assert.Equal(SchemaVersions.AppConfig, migrated.SchemaVersion);
        Assert.Equal(AppPaths.DefaultSubscriptionCheckIntervalMinutes, migrated.SubscriptionCheckIntervalMinutes);
        Assert.True(migrated.RunInBackground);
        Assert.False(migrated.StartAtLogin);
    }

    [Fact]
    public void MigrateProfile_UpgradesSchemaVersion()
    {
        var profile = new DownloadProfile { Id = "p", Name = "P", SchemaVersion = 0 };
        var migrated = SettingsMigration.MigrateProfile(profile);
        Assert.Equal(SchemaVersions.Profile, migrated.SchemaVersion);
    }

    private const string LegacyTemplate = "%(title)s [%(id)s].%(ext)s";

    [Fact]
    public void MigrateProfile_OldBuiltInWithLegacyOutputTemplate_GetsTheBuiltInTemplateOnce()
    {
        var profile = new DownloadProfile { Id = AppPaths.AudioMp3ProfileId, Name = "Audio", SchemaVersion = 1 };
        profile.Options["-o"] = LegacyTemplate;

        SettingsMigration.MigrateProfile(profile);

        Assert.Equal("%(channel)s/%(title)s.%(ext)s", profile.Options["-o"]);
    }

    [Fact]
    public void MigrateProfile_CurrentVersion_KeepsAnExplicitLegacyTemplate()
    {
        var profile = new DownloadProfile { Id = AppPaths.AudioMp3ProfileId, Name = "Audio" };
        profile.Options["-o"] = LegacyTemplate;

        SettingsMigration.MigrateProfile(profile);

        Assert.Equal(LegacyTemplate, profile.Options["-o"]);
    }

    [Fact]
    public async Task ResetOutputTemplate_SurvivesTheNextStart()
    {
        var root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProfileStore(root);
            var config = new AppConfigStore(root, store);
            await config.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
            var profile = (await store.GetAsync(AppPaths.AudioMp3ProfileId, TestContext.Current.CancellationToken))!;
            profile.Options["-o"] = LegacyTemplate;
            await store.SaveAsync(profile, TestContext.Current.CancellationToken);

            await new AppConfigStore(root, new ProfileStore(root)).EnsureBootstrapAsync(TestContext.Current.CancellationToken);

            var reloaded = (await store.GetAsync(AppPaths.AudioMp3ProfileId, TestContext.Current.CancellationToken))!;
            Assert.Equal(LegacyTemplate, reloaded.Options["-o"]?.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
