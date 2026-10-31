// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpSearchResultParserTests
{
    private readonly YtDlpSearchResultParser _parser = new();

    [Fact]
    public void Parse_MultipleEntries_ReturnsAllResults()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "abc123",
                  "title": "First Video",
                  "channel": "Channel One",
                  "webpage_url": "https://www.youtube.com/watch?v=abc123",
                  "thumbnails": [
                    { "url": "https://i.ytimg.com/vi/abc123/hqdefault.jpg", "width": 480 }
                  ]
                },
                {
                  "id": "def456",
                  "title": "Second Video",
                  "uploader": "Uploader Two"
                }
              ]
            }
            """;

        var results = _parser.Parse(json);

        Assert.Equal(2, results.Count);
        Assert.Equal("abc123", results[0].VideoId);
        Assert.Equal("First Video", results[0].Title);
        Assert.Equal("Channel One", results[0].Channel);
        Assert.Equal("https://www.youtube.com/watch?v=abc123", results[0].WatchUrl);
        Assert.Equal("def456", results[1].VideoId);
        Assert.Equal("Uploader Two", results[1].Channel);
        Assert.Contains("def456", results[1].ThumbnailUrl ?? string.Empty);
    }

    [Fact]
    public void Parse_SingleVideoObject_ReturnsOneResult()
    {
        const string json = """
            {
              "id": "solo1",
              "title": "Solo",
              "uploader": "Solo Channel"
            }
            """;

        var results = _parser.Parse(json);

        Assert.Single(results);
        Assert.Equal("solo1", results[0].VideoId);
        Assert.Equal("https://www.youtube.com/watch?v=solo1", results[0].WatchUrl);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmpty()
    {
        Assert.Empty(_parser.Parse("not json"));
        Assert.Empty(_parser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_EntryWithoutId_IsSkipped()
    {
        const string json = """{ "entries": [ { "title": "No id" } ] }""";

        Assert.Empty(_parser.Parse(json));
    }

    [Fact]
    public void Parse_NumericDuration_ParsesDurationSeconds()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "abc123",
                  "title": "Timed Video",
                  "duration": 176.4
                }
              ]
            }
            """;

        var results = _parser.Parse(json);

        Assert.Single(results);
        Assert.Equal(176, results[0].DurationSeconds);
    }

    [Fact]
    public void Parse_DurationString_ParsesDurationSeconds()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "abc123",
                  "title": "Timed Video",
                  "duration_string": "1:02:15"
                }
              ]
            }
            """;

        var results = _parser.Parse(json);

        Assert.Single(results);
        Assert.Equal(3735, results[0].DurationSeconds);
    }

    [Fact]
    public void Parse_MissingDuration_LeavesDurationNull()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "abc123",
                  "title": "Untimed Video"
                }
              ]
            }
            """;

        var results = _parser.Parse(json);

        Assert.Single(results);
        Assert.Null(results[0].DurationSeconds);
    }

    [Fact]
    public void Parse_LiveStatusAndAvailability_ParsesBothFields()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "live1",
                  "title": "Premiere",
                  "live_status": "is_upcoming",
                  "availability": "subscriber_only"
                }
              ]
            }
            """;

        var results = _parser.Parse(json);

        Assert.Single(results);
        Assert.Equal("is_upcoming", results[0].LiveStatus);
        Assert.Equal("subscriber_only", results[0].Availability);
    }

    [Fact]
    public void Parse_MissingLiveStatusAndAvailability_LeavesThemNull()
    {
        const string json = """{ "entries": [ { "id": "plain1", "title": "Plain" } ] }""";

        var results = _parser.Parse(json);

        Assert.Single(results);
        Assert.Null(results[0].LiveStatus);
        Assert.Null(results[0].Availability);
    }
}
