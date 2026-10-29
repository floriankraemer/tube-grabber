// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class YtDlpChannelFeedParserTests
{
    private readonly YtDlpChannelFeedParser _parser = new();

    [Fact]
    public void Parse_ChannelVideosTab_ReturnsChannelAndEntries()
    {
        const string json = """
            {
              "_type": "playlist",
              "id": "UCxyz123",
              "channel_id": "UCxyz123",
              "channel": "Example Channel",
              "title": "Example Channel - Videos",
              "entries": [
                {
                  "_type": "url",
                  "id": "vid00000001",
                  "title": "Regular Upload",
                  "channel": "Example Channel",
                  "duration": 615,
                  "webpage_url": "https://www.youtube.com/watch?v=vid00000001"
                },
                {
                  "_type": "url",
                  "id": "vid00000002",
                  "title": "Upcoming Premiere",
                  "channel": "Example Channel",
                  "live_status": "is_upcoming",
                  "webpage_url": "https://www.youtube.com/watch?v=vid00000002"
                },
                {
                  "_type": "url",
                  "id": "vid00000003",
                  "title": "Members Only Episode",
                  "channel": "Example Channel",
                  "availability": "subscriber_only",
                  "webpage_url": "https://www.youtube.com/watch?v=vid00000003"
                }
              ]
            }
            """;

        var feed = _parser.Parse(json);

        Assert.NotNull(feed);
        Assert.Equal("UCxyz123", feed.ChannelId);
        Assert.Equal("Example Channel", feed.ChannelName);
        Assert.Equal(3, feed.Entries.Count);
        Assert.Equal("vid00000001", feed.Entries[0].VideoId);
        Assert.Equal(615, feed.Entries[0].DurationSeconds);
        Assert.Equal("is_upcoming", feed.Entries[1].LiveStatus);
        Assert.Equal("subscriber_only", feed.Entries[2].Availability);
    }

    [Fact]
    public void Parse_NestedTabPlaylists_FlattensTabEntries()
    {
        const string json = """
            {
              "_type": "playlist",
              "id": "UCxyz123",
              "channel_id": "UCxyz123",
              "channel": "Example Channel",
              "entries": [
                {
                  "_type": "playlist",
                  "id": "UCxyz123.videos",
                  "title": "Example Channel - Videos",
                  "entries": [
                    { "id": "vid00000001", "title": "First" },
                    { "id": "vid00000002", "title": "Second" }
                  ]
                },
                {
                  "_type": "playlist",
                  "id": "UCxyz123.shorts",
                  "title": "Example Channel - Shorts",
                  "entries": [
                    { "id": "shrt0000001", "title": "Short" }
                  ]
                }
              ]
            }
            """;

        var feed = _parser.Parse(json);

        Assert.NotNull(feed);
        Assert.Equal("UCxyz123", feed.ChannelId);
        Assert.Equal(new[] { "vid00000001", "vid00000002", "shrt0000001" }, feed.Entries.Select(e => e.VideoId));
    }

    [Fact]
    public void Parse_TitleWithVideosSuffix_StripsSuffixWhenChannelNameMissing()
    {
        const string json = """
            {
              "channel_id": "UCxyz123",
              "title": "Example Channel - Videos",
              "entries": []
            }
            """;

        var feed = _parser.Parse(json);

        Assert.NotNull(feed);
        Assert.Equal("Example Channel", feed.ChannelName);
        Assert.Empty(feed.Entries);
    }

    [Fact]
    public void Parse_NoChannelName_FallsBackToChannelId()
    {
        const string json = """{ "id": "UCxyz123", "entries": [] }""";

        var feed = _parser.Parse(json);

        Assert.NotNull(feed);
        Assert.Equal("UCxyz123", feed.ChannelId);
        Assert.Equal("UCxyz123", feed.ChannelName);
    }

    [Fact]
    public void Parse_ChannelIdMissing_UsesUploaderIdThenId()
    {
        const string withUploaderId = """{ "uploader_id": "UCuploader", "id": "UCid", "entries": [] }""";
        const string withIdOnly = """{ "id": "UCid", "entries": [] }""";

        Assert.Equal("UCuploader", _parser.Parse(withUploaderId)?.ChannelId);
        Assert.Equal("UCid", _parser.Parse(withIdOnly)?.ChannelId);
    }

    [Fact]
    public void Parse_MissingChannelId_ReturnsNull()
    {
        const string json = """{ "channel": "No Id Channel", "entries": [] }""";

        Assert.Null(_parser.Parse(json));
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        Assert.Null(_parser.Parse("not json"));
        Assert.Null(_parser.Parse("{ \"channel_id\": "));
        Assert.Null(_parser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_JsonArray_ReturnsNull()
    {
        Assert.Null(_parser.Parse("[{ \"channel_id\": \"UCxyz123\" }]"));
    }
}
