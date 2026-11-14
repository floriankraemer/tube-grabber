// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class TileGridLayoutTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(240, 1)]
    [InlineData(495, 1)]
    [InlineData(496, 2)]
    [InlineData(1000, 3)]
    [InlineData(1316, 5)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Columns_FitAsManyMinimumWidthTilesAsPossible(double width, int expected) =>
        Assert.Equal(expected, TileGridLayout.Columns(width, 240, 16));

    [Fact]
    public void TileWidth_SplitsTheRowEvenlyInWholePixels()
    {
        Assert.Equal(250, TileGridLayout.TileWidth(1316, 5, 16));
        Assert.Equal(322, TileGridLayout.TileWidth(1000, 3, 16));
        Assert.Equal(0, TileGridLayout.TileWidth(0, 1, 16));
    }
}
