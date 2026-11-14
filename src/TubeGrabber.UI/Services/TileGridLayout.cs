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

    /// <summary>Whole pixels, so neighbouring tiles never leave a hairline gap; the remainder stays at the row end.</summary>
    public static double TileWidth(double availableWidth, int columns, double spacing) =>
        Math.Max(0, Math.Floor((availableWidth - spacing * (columns - 1)) / columns));
}
