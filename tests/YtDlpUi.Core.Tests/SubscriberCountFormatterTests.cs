// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class SubscriberCountFormatterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData(-1L, "")]
    [InlineData(0L, "0 subscribers")]
    [InlineData(1L, "1 subscriber")]
    [InlineData(999L, "999 subscribers")]
    [InlineData(1_500L, "1.5K subscribers")]
    [InlineData(20_000L, "20K subscribers")]
    [InlineData(563_000L, "563K subscribers")]
    [InlineData(2_970_000L, "2.97M subscribers")]
    [InlineData(21_400_000L, "21.4M subscribers")]
    [InlineData(1_200_000_000L, "1.2B subscribers")]
    public void Format_AbbreviatesLikeYouTube(long? count, string expected) =>
        Assert.Equal(expected, SubscriberCountFormatter.Format(count));
}
