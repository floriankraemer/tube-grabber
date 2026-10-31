// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class AppConfigStoreCreateDefaultTests
{
    [Fact]
    public void NewAppConfiguration_DefaultsThemePreferenceToSystem()
    {
        var config = new AppConfiguration();
        Assert.Equal(ThemePreference.System, config.ThemePreference);
    }

    [Fact]
    public void CreateDefault_UsesProfileStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(root);
        var store = AppConfigStore.CreateDefault(profiles);
        Assert.EndsWith(".tube-grabber", store.ConfigRoot);
    }
}
