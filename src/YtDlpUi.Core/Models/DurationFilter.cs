// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

/// <summary>Values match YouTube's own "Duration" search filter.</summary>
public enum DurationFilter
{
    Any = 0,
    UnderFourMinutes = 1,
    OverTwentyMinutes = 2,
    FourToTwentyMinutes = 3,
}
