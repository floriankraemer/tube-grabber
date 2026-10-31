// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class DurationFormatterTests
{
    [Theory]
    [InlineData(42, "0:42")]
    [InlineData(725, "12:05")]
    [InlineData(3735, "1:02:15")]
    public void Format_ValidSeconds_ReturnsYouTubeStyle(int seconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(seconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Format_InvalidOrMissing_ReturnsEmpty(int? seconds)
    {
        Assert.Equal(string.Empty, DurationFormatter.Format(seconds));
    }
}
