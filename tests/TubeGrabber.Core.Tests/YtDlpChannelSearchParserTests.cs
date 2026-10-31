// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpChannelSearchParserTests
{
    private readonly YtDlpChannelSearchParser _parser = new();

    [Fact]
    public void Parse_ChannelEntry_ReadsAllFields()
    {
        const string json = """
            {
              "entries": [
                {
                  "_type": "url",
                  "ie_key": "YoutubeTab",
                  "id": "UCHnyfMqiRRG1u-2MsSQLbXA",
                  "title": "Veritasium",
                  "channel_id": "UCHnyfMqiRRG1u-2MsSQLbXA",
                  "channel_url": "https://www.youtube.com/channel/UCHnyfMqiRRG1u-2MsSQLbXA",
                  "uploader_id": "@veritasium",
                  "channel_follower_count": 21400000,
                  "thumbnails": [
                    { "url": "//yt3.ggpht.com/small=s88", "width": 88 },
                    { "url": "//yt3.ggpht.com/large=s176", "width": 176 }
                  ]
                }
              ]
            }
            """;

        var result = Assert.Single(_parser.Parse(json));

        Assert.Equal("UCHnyfMqiRRG1u-2MsSQLbXA", result.ChannelId);
        Assert.Equal("Veritasium", result.Name);
        Assert.Equal("https://www.youtube.com/channel/UCHnyfMqiRRG1u-2MsSQLbXA", result.ChannelUrl);
        Assert.Equal("@veritasium", result.Handle);
        Assert.Equal(21_400_000, result.SubscriberCount);
        Assert.Equal("https://yt3.ggpht.com/large=s176", result.ThumbnailUrl);
    }

    [Fact]
    public void Parse_SparseEntry_FallsBackToIdBasedDefaults()
    {
        const string json = """{ "entries": [ { "id": "UCabc" }, { "title": "no id" } ] }""";

        var result = Assert.Single(_parser.Parse(json));

        Assert.Equal("UCabc", result.Name);
        Assert.Equal("https://www.youtube.com/channel/UCabc", result.ChannelUrl);
        Assert.Null(result.Handle);
        Assert.Null(result.SubscriberCount);
        Assert.Null(result.ThumbnailUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ broken")]
    [InlineData("{}")]
    public void Parse_InvalidOutput_ReturnsEmpty(string output) =>
        Assert.Empty(_parser.Parse(output));
}
