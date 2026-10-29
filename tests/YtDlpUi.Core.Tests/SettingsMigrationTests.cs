// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

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
}
