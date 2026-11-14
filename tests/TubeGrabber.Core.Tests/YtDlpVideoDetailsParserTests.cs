// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpVideoDetailsParserTests
{
    private readonly YtDlpVideoDetailsParser _parser = new();

    [Fact]
    public void Parse_MapsMetadataAndPicksSplitPair()
    {
        const string json = """
            {
              "id": "abc", "title": "T", "channel": "C", "channel_url": "https://yt/c",
              "description": "D", "view_count": 1234, "upload_date": "20240131",
              "duration": 61.4, "thumbnail": "https://i/t.jpg",
              "formats": [
                { "url": "https://sb", "format_note": "storyboard", "vcodec": "none", "acodec": "none", "ext": "mhtml" },
                { "url": "https://v720", "vcodec": "avc1.4d401f", "acodec": "none", "ext": "mp4", "height": 720, "tbr": 1000 },
                { "url": "https://v1080", "vcodec": "avc1.640028", "acodec": "none", "ext": "mp4", "height": 1080, "tbr": 2000 },
                { "url": "https://v2160", "vcodec": "vp09", "acodec": "none", "ext": "webm", "height": 2160, "tbr": 9000 },
                { "url": "https://a-webm", "vcodec": "none", "acodec": "opus", "ext": "webm", "abr": 160 },
                { "url": "https://a-m4a", "vcodec": "none", "acodec": "mp4a.40.2", "ext": "m4a", "abr": 128 }
              ]
            }
            """;

        var details = _parser.Parse(json);

        Assert.Equal("abc", details.Id);
        Assert.Equal("T", details.Title);
        Assert.Equal("C", details.Channel);
        Assert.Equal("https://yt/c", details.ChannelUrl);
        Assert.Equal("D", details.Description);
        Assert.Equal(1234, details.ViewCount);
        Assert.Equal(new DateOnly(2024, 1, 31), details.UploadDate);
        Assert.Equal(61, details.DurationSeconds);
        Assert.Equal("https://i/t.jpg", details.ThumbnailUrl);
        Assert.Equal("https://v1080", details.VideoStreamUrl);
        Assert.Equal("https://a-m4a", details.AudioStreamUrl);
    }

    [Fact]
    public void Parse_PickedFormatsHaveAvailableAt_ExposesTheLatestOne()
    {
        const string json = """
            {
              "id": "abc",
              "formats": [
                { "url": "https://v", "vcodec": "avc1.640028", "acodec": "none", "ext": "mp4", "height": 1080, "available_at": 1794663468 },
                { "url": "https://a", "vcodec": "none", "acodec": "mp4a.40.2", "ext": "m4a", "abr": 128, "available_at": 1794663470.4 },
                { "url": "https://other", "vcodec": "avc1.4d401f", "acodec": "none", "ext": "mp4", "height": 720, "available_at": 1794669999 }
              ]
            }
            """;

        var details = _parser.Parse(json);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794663471), details.StreamsAvailableAt);
    }

    [Fact]
    public void Parse_NoAvailableAt_LeavesItNull()
    {
        const string json = """
            { "id": "abc", "formats": [ { "url": "https://m", "vcodec": "avc1", "acodec": "mp4a", "ext": "mp4", "height": 360 } ] }
            """;

        Assert.Null(_parser.Parse(json).StreamsAvailableAt);
    }

    [Fact]
    public void Parse_OnlyAbove1080_FallsBackToMuxed()
    {
        const string json = """
            { "id": "x", "formats": [
              { "url": "https://v2160", "vcodec": "vp09", "acodec": "none", "height": 2160 },
              { "url": "https://a", "vcodec": "none", "acodec": "opus", "abr": 100 },
              { "url": "https://m360", "vcodec": "avc1", "acodec": "mp4a", "height": 360, "tbr": 300 },
              { "url": "https://m720", "vcodec": "avc1", "acodec": "mp4a", "height": 720, "tbr": 900 }
            ] }
            """;

        var details = _parser.Parse(json);

        Assert.Equal("https://m720", details.VideoStreamUrl);
        Assert.Null(details.AudioStreamUrl);
    }

    [Fact]
    public void Parse_MissingFields_AreTolerated()
    {
        var details = _parser.Parse("""{ "id": "only" }""");

        Assert.Equal("only", details.Title);
        Assert.Null(details.Channel);
        Assert.Null(details.ViewCount);
        Assert.Null(details.UploadDate);
        Assert.Null(details.DurationSeconds);
        Assert.Null(details.VideoStreamUrl);
        Assert.Null(details.AudioStreamUrl);
    }

    [Fact]
    public void Parse_InvalidUploadDateAndOddFormats_AreIgnored()
    {
        const string json = """
            { "id": "x", "upload_date": "nope", "view_count": "many", "formats": [ 5, { "vcodec": "avc1", "acodec": "mp4a" } ] }
            """;

        var details = _parser.Parse(json);

        Assert.Null(details.UploadDate);
        Assert.Null(details.ViewCount);
        Assert.Null(details.VideoStreamUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ \"id\": ")]
    [InlineData("{ \"title\": \"no id\" }")]
    public void Parse_Malformed_ThrowsClearError(string output)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _parser.Parse(output));

        Assert.StartsWith("yt-dlp", ex.Message);
    }
}
