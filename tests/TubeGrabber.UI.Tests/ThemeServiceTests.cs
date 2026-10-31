// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Styling;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void MapToVariant_System_ReturnsDefault() =>
        Assert.Equal(ThemeVariant.Default, ThemeService.MapToVariant(ThemePreference.System));

    [Fact]
    public void MapToVariant_Light_ReturnsLight() =>
        Assert.Equal(ThemeVariant.Light, ThemeService.MapToVariant(ThemePreference.Light));

    [Fact]
    public void MapToVariant_Dark_ReturnsDark() =>
        Assert.Equal(ThemeVariant.Dark, ThemeService.MapToVariant(ThemePreference.Dark));
}
