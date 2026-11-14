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

    [Theory]
    [InlineData(1313, 5, 8)]
    [InlineData(1316, 5, 16)]
    [InlineData(1000, 3, 16)]
    [InlineData(801, 3, 8)]
    [InlineData(250, 1, 8)]
    public void TileLeft_LastTileEndsExactlyAtTheRightEdge(double width, int columns, double spacing)
    {
        var tile = TileGridLayout.TileWidth(width, columns, spacing);

        var lastRight = TileGridLayout.TileLeft(columns - 1, columns, width, tile, spacing) + tile;

        Assert.Equal(columns == 1 ? tile : width, lastRight);
        Assert.Equal(0, TileGridLayout.TileLeft(0, columns, width, tile, spacing));
    }

    [Fact]
    public void TileLeft_GapsDifferByAtMostOnePixel()
    {
        const double width = 1313;
        var tile = TileGridLayout.TileWidth(width, 5, 8);

        var gaps = Enumerable.Range(1, 4)
            .Select(i => TileGridLayout.TileLeft(i, 5, width, tile, 8) - TileGridLayout.TileLeft(i - 1, 5, width, tile, 8) - tile)
            .ToList();

        Assert.True(gaps.Max() - gaps.Min() <= 1);
        Assert.All(gaps, gap => Assert.InRange(gap, 8, 9));
    }
}
