// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

/// <summary>Size of the 16:9 player on the watch page.</summary>
public static class PlayerLayout
{
    public const double MaxWidth = 1280;
    public const double MaxHeightShare = 0.7;

    // 16 px steps give a whole-pixel height at exactly 16:9. Any other width makes the video surface round its
    // picture down and leave a black hairline at the edge.
    private const double Step = 16;

    /// <summary>As wide as fits (up to <see cref="MaxWidth"/>), at most <see cref="MaxHeightShare"/> of the viewport tall.</summary>
    public static (double Width, double Height) Size(double availableWidth, double viewportHeight)
    {
        var width = Math.Max(0, Math.Min(MaxWidth, availableWidth));
        var maxHeight = Math.Max(0, viewportHeight * MaxHeightShare);
        if (width * 9 / 16 > maxHeight)
            width = maxHeight * 16 / 9;

        width = Math.Floor(width / Step) * Step;
        return (width, width * 9 / 16);
    }
}
