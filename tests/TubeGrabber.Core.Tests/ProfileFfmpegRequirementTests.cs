// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ProfileFfmpegRequirementTests
{
    [Fact]
    public void RequiresFfmpeg_WhenAudioExtractEnabled()
    {
        var profile = BuiltInProfiles.CreateAudioMp3();
        Assert.True(ProfileFfmpegRequirement.RequiresFfmpeg(profile));
    }

    [Fact]
    public void RequiresFfmpeg_WhenExtraArgsContainExtractFlag()
    {
        var profile = new DownloadProfile
        {
            Id = "custom",
            Name = "Custom",
            ExtraArgs = "-x --audio-format mp3",
        };

        Assert.True(ProfileFfmpegRequirement.RequiresFfmpeg(profile));
    }

    [Fact]
    public void RequiresFfmpeg_IsFalseForBestAudioOnly()
    {
        var profile = new DownloadProfile
        {
            Id = "custom",
            Name = "Custom",
            Options = { ["-f"] = "bestaudio" },
        };

        Assert.False(ProfileFfmpegRequirement.RequiresFfmpeg(profile));
    }
}
