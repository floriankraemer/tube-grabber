// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class AppServicesTests
{
    [Fact]
    public void CreateMainViewModel_ReturnsConfiguredInstance()
    {
        var services = new AppServices();
        var vm = services.CreateMainViewModel();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CreateSubscriptionsViewModel_ReturnsConfiguredInstance()
    {
        var services = new AppServices();
        using var vm = services.CreateSubscriptionsViewModel();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CreateSettingsViewModel_ReturnsConfiguredInstance()
    {
        var services = new AppServices();
        var vm = services.CreateSettingsViewModel();
        Assert.NotNull(vm);
    }
}
