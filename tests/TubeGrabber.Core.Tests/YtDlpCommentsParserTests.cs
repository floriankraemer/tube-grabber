// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpCommentsParserTests
{
    private readonly YtDlpCommentsParser _parser = new();

    [Fact]
    public void Parse_KeepsRootCommentsAndDropsReplies()
    {
        const string json = """
            {
              "comments": [
                { "id": "root1", "parent": "root", "author": "Alice", "text": "First" },
                { "id": "reply1", "parent": "root1", "author": "Bob", "text": "Reply" },
                { "id": "root2", "parent": "root", "author": "Carol", "text": "Second" }
              ]
            }
            """;

        var comments = _parser.Parse(json);

        Assert.Equal(["root1", "root2"], comments.Select(c => c.Id));
    }

    [Fact]
    public void Parse_MapsAllFields()
    {
        const string json = """
            {
              "comments": [
                {
                  "id": "c1",
                  "parent": "root",
                  "author": "Uploader",
                  "text": "Pinned words",
                  "like_count": 42,
                  "timestamp": 1700000000,
                  "is_pinned": true,
                  "author_is_uploader": true
                }
              ]
            }
            """;

        var comment = Assert.Single(_parser.Parse(json));

        Assert.Equal("c1", comment.Id);
        Assert.Equal("Uploader", comment.Author);
        Assert.Equal("Pinned words", comment.Text);
        Assert.Equal(42L, comment.LikeCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), comment.PostedAt);
        Assert.True(comment.IsPinned);
        Assert.True(comment.AuthorIsUploader);
    }

    [Fact]
    public void Parse_MissingOptionalFields_UsesDefaults()
    {
        const string json = """
            { "comments": [ { "id": "c1", "parent": "root" } ] }
            """;

        var comment = Assert.Single(_parser.Parse(json));

        Assert.Equal(string.Empty, comment.Author);
        Assert.Equal(string.Empty, comment.Text);
        Assert.Null(comment.LikeCount);
        Assert.Null(comment.PostedAt);
        Assert.False(comment.IsPinned);
        Assert.False(comment.AuthorIsUploader);
    }

    [Fact]
    public void Parse_NullLikeCountAndTimestamp_AreNull()
    {
        const string json = """
            { "comments": [ { "id": "c1", "parent": "root", "like_count": null, "timestamp": null } ] }
            """;

        var comment = Assert.Single(_parser.Parse(json));

        Assert.Null(comment.LikeCount);
        Assert.Null(comment.PostedAt);
    }

    [Fact]
    public void Parse_OutOfRangeTimestamp_IsIgnored()
    {
        const string json = """
            { "comments": [ { "id": "c1", "parent": "root", "timestamp": 9223372036854775807 } ] }
            """;

        var comment = Assert.Single(_parser.Parse(json));

        Assert.Null(comment.PostedAt);
    }

    [Fact]
    public void Parse_EntryWithoutId_IsSkipped()
    {
        const string json = """
            { "comments": [ { "parent": "root", "text": "No id" }, "not an object", { "id": "ok", "parent": "root" } ] }
            """;

        Assert.Equal(["ok"], _parser.Parse(json).Select(c => c.Id));
    }

    [Theory]
    [InlineData("{ \"comments\": null }")]
    [InlineData("{ \"comments\": [] }")]
    [InlineData("{ \"comments\": \"disabled\" }")]
    [InlineData("{ \"title\": \"no comments key\" }")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[1, 2, 3]")]
    public void Parse_NoUsableComments_ReturnsEmpty(string output)
    {
        Assert.Empty(_parser.Parse(output));
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsEmpty()
    {
        Assert.Empty(_parser.Parse("{ \"comments\": [ { \"id\": "));
    }
}
