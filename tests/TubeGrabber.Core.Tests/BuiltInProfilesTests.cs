// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class BuiltInProfilesTests
{
    [Fact]
    public void All_ContainsThreeBuiltInProfiles()
    {
        var ids = BuiltInProfiles.All.Select(profile => profile.Id).ToList();

        Assert.Equal(3, ids.Count);
        Assert.Contains(AppPaths.DefaultProfileId, ids);
        Assert.Contains(AppPaths.AudioMp3ProfileId, ids);
        Assert.Contains(AppPaths.HqVideoProfileId, ids);
    }

    [Fact]
    public void Build_AudioMp3_IncludesExtractAndMp3Flags()
    {
        var catalog = new YtDlpOptionCatalog();
        var builder = new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer());
        var args = builder.Build(BuiltInProfiles.CreateAudioMp3(), null, "https://www.youtube.com/watch?v=abc");

        Assert.Contains("-f", args);
        Assert.Contains("bestaudio", args);
        Assert.Contains("-x", args);
        Assert.Contains("--audio-format", args);
        Assert.Contains("mp3", args);
        Assert.Contains("--sponsorblock-remove", args);
        Assert.Contains("--force-overwrites", args);
    }

    [Fact]
    public void Build_HqVideo_PrefersSingleMp4AndMergeFormat()
    {
        var catalog = new YtDlpOptionCatalog();
        var builder = new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer());
        var args = builder.Build(BuiltInProfiles.CreateHqVideo(), null, "https://www.youtube.com/watch?v=abc");

        Assert.Contains(
            "b[ext=mp4]/bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[ext=mp4]/b",
            args);
        Assert.Contains("--merge-output-format", args);
        Assert.Contains("mp4", args);
        Assert.Contains("res:1080,vcodec:h264,acodec:aac", args);
    }

    [Fact]
    public void Build_Default_PrefersProgressiveMp4BeforeMerge()
    {
        var catalog = new YtDlpOptionCatalog();
        var builder = new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer());
        var args = builder.Build(BuiltInProfiles.CreateDefault(), null, "https://www.youtube.com/watch?v=abc");

        Assert.Contains("b[ext=mp4]/bv*+ba/b", args);
        Assert.Contains("--merge-output-format", args);
    }
}
