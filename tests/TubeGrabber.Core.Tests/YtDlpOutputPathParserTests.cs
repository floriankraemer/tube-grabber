// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpOutputPathParserTests
{
    private readonly YtDlpOutputPathParser _parser = new();

    [Theory]
    [InlineData("[download] Destination: My Video.mp4", "My Video.mp4")]
    [InlineData("[Merger] Merging formats into \"My Video.mp4\"", "My Video.mp4")]
    [InlineData("[ExtractAudio] Destination: /tmp/audio.mp3", "/tmp/audio.mp3")]
    [InlineData("[MoveFiles] Moving file \"a.mp4\" to \"b.mp4\"", "b.mp4")]
    [InlineData("[Metadata] Adding metadata to \"clip.mp4\"", "clip.mp4")]
    public void TryExtractPath_ParsesKnownYtDlpLines(string line, string expected)
    {
        Assert.True(_parser.TryExtractPath(line, out var path));
        Assert.Equal(expected, path);
    }

    [Fact]
    public void TryAddCandidate_AccumulatesMultiplePaths()
    {
        var paths = new List<string>();

        Assert.True(_parser.TryAddCandidate("[download] Destination: part-one.mp4", paths));
        Assert.True(_parser.TryAddCandidate("[Merger] Merging formats into \"final.mp4\"", paths));

        Assert.Equal(2, paths.Count);
        Assert.Equal("final.mp4", paths[^1]);
    }

    [Fact]
    public void TryExtractPath_IgnoresUnrelatedLines()
    {
        Assert.False(_parser.TryExtractPath("download:PROGRESS=50%|SPEED=1MiB/s|ETA=00:10", out _));
        Assert.False(_parser.TryExtractPath("[info] Downloading video", out _));
    }
}
