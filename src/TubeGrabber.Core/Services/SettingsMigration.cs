// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class SettingsMigration
{
    private const string LegacyOutputTemplate = "%(title)s [%(id)s].%(ext)s";

    public static AppConfiguration MigrateAppConfig(AppConfiguration config)
    {
        if (config.SchemaVersion >= SchemaVersions.AppConfig)
            return config;

        if (config.SchemaVersion < 3)
            config.ThemePreference = ThemePreference.System;

        if (config.SchemaVersion < 4)
        {
            config.SubscriptionCheckIntervalMinutes = AppPaths.DefaultSubscriptionCheckIntervalMinutes;
            config.RunInBackground = true;
            config.StartAtLogin = false;
        }

        config.SchemaVersion = SchemaVersions.AppConfig;
        return config;
    }

    public static DownloadProfile MigrateProfile(DownloadProfile profile)
    {
        if (profile.SchemaVersion >= SchemaVersions.Profile)
            return profile;

        // Version 2: built-ins saved by older versions got the title-only output template. Upgrade it once;
        // afterwards the value is the user's choice and the synchroniser leaves it alone.
        if (profile.SchemaVersion < 2
            && BuiltInProfiles.FindTemplate(profile.Id)?.Options.GetValueOrDefault("-o") is string templateOutput
            && (!profile.Options.TryGetValue("-o", out var current)
                || string.IsNullOrWhiteSpace(current?.ToString())
                || string.Equals(current.ToString(), LegacyOutputTemplate, StringComparison.Ordinal)))
            profile.Options["-o"] = templateOutput;

        profile.SchemaVersion = SchemaVersions.Profile;
        return profile;
    }
}
