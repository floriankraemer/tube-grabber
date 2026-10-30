// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class YouTubeSearchFilterEncoderTests
{
    [Theory]
    [InlineData(UploadDateFilter.Any, DurationFilter.Any, "EgIQAQ%3D%3D")]
    [InlineData(UploadDateFilter.LastHour, DurationFilter.Any, "EgQIARAB")]
    [InlineData(UploadDateFilter.ThisWeek, DurationFilter.Any, "EgQIAxAB")]
    [InlineData(UploadDateFilter.Any, DurationFilter.UnderFourMinutes, "EgQQARgB")]
    [InlineData(UploadDateFilter.Any, DurationFilter.OverTwentyMinutes, "EgQQARgC")]
    [InlineData(UploadDateFilter.Any, DurationFilter.FourToTwentyMinutes, "EgQQARgD")]
    [InlineData(UploadDateFilter.ThisYear, DurationFilter.UnderFourMinutes, "EgYIBRABGAE%3D")]
    public void BuildVideoSearchUrl_EncodesFilters(UploadDateFilter date, DurationFilter duration, string expectedSp)
    {
        var url = YouTubeSearchFilterEncoder.BuildVideoSearchUrl("cats", new SearchFilters(date, duration));

        Assert.Equal($"https://www.youtube.com/results?search_query=cats&sp={expectedSp}", url);
    }

    [Fact]
    public void BuildChannelSearchUrl_UsesChannelTypeFilter() =>
        Assert.Equal(
            "https://www.youtube.com/results?search_query=veritasium&sp=EgIQAg%3D%3D",
            YouTubeSearchFilterEncoder.BuildChannelSearchUrl("veritasium"));

    [Fact]
    public void BuildVideoSearchUrl_EscapesQuery()
    {
        var url = YouTubeSearchFilterEncoder.BuildVideoSearchUrl("a&b c#d", SearchFilters.Default);

        Assert.Contains("search_query=a%26b+c%23d&", url);
    }
}
