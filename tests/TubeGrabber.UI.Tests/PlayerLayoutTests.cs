// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class PlayerLayoutTests
{
    [Fact]
    public void Size_FillsTheWidthUpToTheMaximum()
    {
        var (width, height) = PlayerLayout.Size(availableWidth: 1316, viewportHeight: 4000);

        Assert.Equal(1280, width);
        Assert.Equal(720, height);
    }

    [Fact]
    public void Size_IsCappedAtSeventyPercentOfTheViewportHeight()
    {
        var (width, height) = PlayerLayout.Size(availableWidth: 1316, viewportHeight: 600);

        Assert.True(height <= 600 * PlayerLayout.MaxHeightShare);
        Assert.Equal(width * 9 / 16, height);
        Assert.Equal(0, width % 16);
    }

    [Theory]
    [InlineData(745, 1000)]
    [InlineData(617, 700)]
    [InlineData(333, 2000)]
    public void Size_IsExactlySixteenByNineInWholePixels(double available, double viewport)
    {
        var (width, height) = PlayerLayout.Size(available, viewport);

        Assert.Equal(Math.Floor(width), width);
        Assert.Equal(Math.Floor(height), height);
        Assert.Equal(width * 9, height * 16);
        Assert.True(width <= available);
    }

    [Fact]
    public void Size_NoRoom_IsEmpty()
    {
        Assert.Equal((0d, 0d), PlayerLayout.Size(-5, -5));
    }
}
