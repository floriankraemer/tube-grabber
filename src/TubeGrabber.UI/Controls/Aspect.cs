// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Data.Converters;

namespace TubeGrabber.UI.Controls;

/// <summary>Binding converters that derive a height from a width, so thumbnails keep their shape while tiles flex.</summary>
public static class Aspect
{
    /// <summary>Width to the height of a 16:9 frame, in whole pixels.</summary>
    public static readonly IValueConverter SixteenByNine = new FuncValueConverter<double, double>(width => Math.Round(width * 9 / 16));
}
