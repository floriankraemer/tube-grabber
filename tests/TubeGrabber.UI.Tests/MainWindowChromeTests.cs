// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using TubeGrabber.UI.Views;

namespace TubeGrabber.UI.Tests;

public sealed class MainWindowChromeTests
{
    [Fact]
    public void ContentMargin_TitleBarOnly_LeavesContentAtTheWindowEdge()
    {
        // Windows and macOS report just the title bar height; the title row is already part of the layout.
        Assert.Equal(default, MainWindow.ContentMargin(new Thickness(0, 44, 0, 0), 44));
    }

    [Fact]
    public void ContentMargin_DrawnFrameAndShadow_InsetsContentByFrameAndShadowOnly()
    {
        // Linux client-side decorations add a 1px frame and an 8px shadow on every side.
        Assert.Equal(new Thickness(9, 9, 9, 9), MainWindow.ContentMargin(new Thickness(9, 53, 9, 9), 44));
    }

    [Fact]
    public void HasBackdropMaterial_OnlyForBlurredMaterials()
    {
        Assert.False(MainWindow.HasBackdropMaterial(WindowTransparencyLevel.None));
        Assert.False(MainWindow.HasBackdropMaterial(WindowTransparencyLevel.Transparent));
        Assert.True(MainWindow.HasBackdropMaterial(WindowTransparencyLevel.Blur));
        Assert.True(MainWindow.HasBackdropMaterial(WindowTransparencyLevel.AcrylicBlur));
        Assert.True(MainWindow.HasBackdropMaterial(WindowTransparencyLevel.Mica));
    }
}
