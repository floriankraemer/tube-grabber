// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YouTubeUrlNormalizerTests
{
    private readonly YouTubeUrlNormalizer _normalizer = new();

    [Fact]
    public void Normalize_StripsYouTubeQueryParams()
    {
        var result = _normalizer.Normalize("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s&list=abc");
        Assert.True(result.IsSuccess);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", result.NormalizedUrl);
    }

    [Fact]
    public void Normalize_StripsYouTuBeShortLinkQuery()
    {
        var result = _normalizer.Normalize("https://youtu.be/dQw4w9WgXcQ?si=abc");
        Assert.True(result.IsSuccess);
        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", result.NormalizedUrl);
    }

    [Fact]
    public void Normalize_LeavesNonYouTubeUrlUnchanged()
    {
        const string url = "https://example.com/video?id=1&ref=x";
        var result = _normalizer.Normalize(url);
        Assert.True(result.IsSuccess);
        Assert.Equal(url, result.NormalizedUrl);
    }

    [Fact]
    public void Normalize_RejectsEmpty()
    {
        var result = _normalizer.Normalize("  ");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Normalize_RejectsNonHttpScheme()
    {
        var result = _normalizer.Normalize("ftp://youtube.com/watch?v=abc");
        Assert.False(result.IsSuccess);
    }
}
