// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

/// <summary>Column maths of the Browse tile grid: as many columns as fit the minimum tile width, tiles share the row evenly.</summary>
public static class TileGridLayout
{
    public static int Columns(double availableWidth, double minTileWidth, double spacing)
    {
        if (double.IsInfinity(availableWidth) || availableWidth <= minTileWidth)
            return 1;

        return Math.Max(1, (int)Math.Floor((availableWidth + spacing) / (minTileWidth + spacing)));
    }

    /// <summary>Whole pixels, so every tile has the same size; <see cref="TileLeft"/> hands the remainder to the gaps.</summary>
    public static double TileWidth(double availableWidth, int columns, double spacing) =>
        Math.Max(0, Math.Floor((availableWidth - spacing * (columns - 1)) / columns));

    /// <summary>
    /// Left edge of the tile at <paramref name="column"/>. The pixels left over by whole-pixel tile widths are spread over
    /// the gaps, so the last tile ends exactly at the right edge and lines up with the header above the grid.
    /// </summary>
    public static double TileLeft(int column, int columns, double availableWidth, double tileWidth, double spacing)
    {
        var left = column * (tileWidth + spacing);
        if (columns < 2 || double.IsInfinity(availableWidth))
            return left;

        var leftover = availableWidth - columns * tileWidth - (columns - 1) * spacing;
        return left + Math.Round(leftover * column / (columns - 1), MidpointRounding.AwayFromZero);
    }
}
