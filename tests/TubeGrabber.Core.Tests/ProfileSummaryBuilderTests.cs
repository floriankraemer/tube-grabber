// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ProfileSummaryBuilderTests
{
    private static readonly YtDlpOptionCatalog Catalog = new();

    private static DownloadProfile Profile(params (string Flag, object? Value)[] options)
    {
        var profile = new DownloadProfile { Id = "p", Name = "P" };
        foreach (var (flag, value) in options)
            profile.Options[flag] = value;
        return profile;
    }

    [Fact]
    public void Build_EmptyProfile_IsYtDlpDefaults() =>
        Assert.Equal("yt-dlp defaults", ProfileSummaryBuilder.Build(Profile(), Catalog));

    [Fact]
    public void Build_OnlyDefaultValues_IsYtDlpDefaults() =>
        Assert.Equal("yt-dlp defaults", ProfileSummaryBuilder.Build(Profile(("-x", false), ("--retries", 10), ("-f", "bv*+ba/b")), Catalog));

    [Fact]
    public void Build_AudioProfile_NamesFormat() =>
        Assert.Equal("Audio only → MP3 · SponsorBlock · 3 options set", ProfileSummaryBuilder.Build(
            Profile(("-x", true), ("--audio-format", "mp3"), ("--sponsorblock-remove", "sponsor")), Catalog));

    [Fact]
    public void Build_AudioWithoutFormat_SaysBest() =>
        Assert.Equal("Audio only → best · 1 option set", ProfileSummaryBuilder.Build(Profile(("-x", true)), Catalog));

    [Fact]
    public void Build_VideoWithSortResolution_ShowsHeightAndContainer() =>
        Assert.Equal("MP4 up to 1080p · 2 options set", ProfileSummaryBuilder.Build(
            Profile(("-S", "vcodec:h264,res:1080"), ("--merge-output-format", "mp4")), Catalog));

    [Fact]
    public void Build_VideoWithHeightInFormat_ShowsHeight() =>
        Assert.Equal("Video up to 720p · 1 option set", ProfileSummaryBuilder.Build(
            Profile(("-f", "bv*[height<=720]+ba/b")), Catalog));

    [Fact]
    public void Build_Credentials_AreSummarisedAsSignedIn() =>
        Assert.Equal("Video · signed in · 1 option set", ProfileSummaryBuilder.Build(Profile(("--cookies", "/tmp/c.txt")), Catalog));

    [Fact]
    public void Build_ExtraArgsOnly_DoesNotClaimDefaults() =>
        Assert.Equal("Video · extra arguments", ProfileSummaryBuilder.Build(
            new DownloadProfile { Id = "p", Name = "P", ExtraArgs = "--embed-chapters" }, Catalog));

    [Fact]
    public void Build_CanLeaveOutTheOptionCount() =>
        Assert.Equal("MP4 up to 1080p", ProfileSummaryBuilder.Build(
            Profile(("-S", "res:1080"), ("--merge-output-format", "mp4")), Catalog, includeCount: false));

    [Fact]
    public void Build_ReadsJsonElementValues()
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<DownloadProfile>(
            """{"Id":"p","Name":"P","Options":{"-x":true,"--audio-format":"m4a"}}""")!;
        Assert.Equal("Audio only → M4A · 2 options set", ProfileSummaryBuilder.Build(json, Catalog));
    }
}
