// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpStreamPickerTests
{
    private static StreamFormat Video(string url, int height, string codec = "avc1", string ext = "mp4", double tbr = 1, string? protocol = "https") =>
        new(url, codec, "none", ext, protocol, null, height, tbr, null);

    private static StreamFormat Audio(string url, string ext = "m4a", double abr = 1, string? protocol = "https") =>
        new(url, "none", "mp4a", ext, protocol, null, null, null, abr);

    [Fact]
    public void Pick_PrefersAvcOverHigherHeightVp9()
    {
        var picked = YtDlpStreamPicker.Pick([Video("vp9", 1080, "vp09", "webm", tbr: 5000), Video("avc", 720), Audio("a")]);

        Assert.Equal("avc", picked.VideoUrl);
    }

    [Fact]
    public void Pick_SameCodec_PrefersHeightThenBitrate()
    {
        var picked = YtDlpStreamPicker.Pick([Video("low", 720, tbr: 9), Video("hi-a", 1080, tbr: 1), Video("hi-b", 1080, tbr: 2), Audio("a")]);

        Assert.Equal("hi-b", picked.VideoUrl);
        Assert.Equal("a", picked.AudioUrl);
    }

    [Fact]
    public void Pick_Audio_PrefersM4aThenBitrate()
    {
        var picked = YtDlpStreamPicker.Pick([Video("v", 720), Audio("opus", "webm", 200), Audio("m4a-lo", "m4a", 48), Audio("m4a-hi", "m4a", 128)]);

        Assert.Equal("m4a-hi", picked.AudioUrl);
    }

    [Fact]
    public void Pick_HlsOnlyUsedWhenNoDirectOption()
    {
        var withDirect = YtDlpStreamPicker.Pick([Video("hls", 1080, protocol: "m3u8_native"), Video("direct", 480), Audio("a")]);
        var hlsOnly = YtDlpStreamPicker.Pick([Video("hls", 1080, protocol: "m3u8_native"), Audio("a")]);

        Assert.Equal("direct", withDirect.VideoUrl);
        Assert.Equal("hls", hlsOnly.VideoUrl);
    }

    [Fact]
    public void Pick_SkipsFormatsWithoutUrlAndStoryboards()
    {
        var story = new StreamFormat("sb", "avc1", "mp4a", "mp4", "https", "storyboard", 90, null, null);
        var noUrl = Video(" ", 1080);

        var picked = YtDlpStreamPicker.Pick([story, noUrl]);

        Assert.Null(picked.VideoUrl);
        Assert.Null(picked.AudioUrl);
    }

    [Fact]
    public void Pick_VideoWithoutAudio_FallsBackToMuxed()
    {
        var muxed = new StreamFormat("muxed", "avc1", "mp4a", "mp4", "https", null, 360, 1, null);

        var picked = YtDlpStreamPicker.Pick([Video("v", 1080), muxed]);

        Assert.Equal("muxed", picked.VideoUrl);
        Assert.Null(picked.AudioUrl);
    }
}
