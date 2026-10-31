// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

public static class DurationFormatter
{
    public static string Format(int? totalSeconds)
    {
        if (totalSeconds is not > 0)
            return string.Empty;

        var seconds = totalSeconds.Value;
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var remainingSeconds = seconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:D2}:{remainingSeconds:D2}"
            : $"{minutes}:{remainingSeconds:D2}";
    }
}
