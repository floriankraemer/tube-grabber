// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Globalization;

namespace YtDlpUi.Core.Services;

public static class SubscriberCountFormatter
{
    public static string Format(long? count)
    {
        if (count is not >= 0)
            return string.Empty;

        var value = count.Value;

        return value == 1 ? "1 subscriber" : $"{Abbreviate(value)} subscribers";
    }

    // Three significant digits, like YouTube itself: 2.97M, 21.4M, 563K.
    private static string Abbreviate(long value)
    {
        var (divisor, suffix) = value switch
        {
            >= 1_000_000_000 => (1_000_000_000d, "B"),
            >= 1_000_000 => (1_000_000d, "M"),
            >= 1_000 => (1_000d, "K"),
            _ => (1d, string.Empty),
        };

        var scaled = value / divisor;
        var format = scaled switch
        {
            < 10 => "0.##",
            < 100 => "0.#",
            _ => "0",
        };

        return scaled.ToString(format, CultureInfo.InvariantCulture) + suffix;
    }
}
