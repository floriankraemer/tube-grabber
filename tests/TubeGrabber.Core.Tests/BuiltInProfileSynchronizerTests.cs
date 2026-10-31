// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class BuiltInProfileSynchronizerTests
{
    [Fact]
    public void MergeMissingFromTemplate_AddsMissingAudioOptions()
    {
        var existing = new DownloadProfile
        {
            Id = AppPaths.AudioMp3ProfileId,
            Name = "Download Audio as mp3",
            Options = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["-f"] = "bestaudio",
                ["--no-part"] = true,
            },
        };

        var changed = BuiltInProfileSynchronizer.MergeMissingFromTemplate(
            existing,
            BuiltInProfiles.CreateAudioMp3());

        Assert.True(changed);
        Assert.True(existing.Options.ContainsKey("-x"));
        Assert.True(existing.Options.ContainsKey("--force-overwrites"));
        Assert.Equal("mp3", existing.Options["--audio-format"]?.ToString());
    }

    [Fact]
    public void MergeMissingFromTemplate_RepairsIncompleteAudioProfile()
    {
        var existing = new DownloadProfile
        {
            Id = AppPaths.AudioMp3ProfileId,
            Name = "Download Audio as mp3",
            Options = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["-f"] = "bestaudio",
                ["--concurrent-fragments"] = 16,
            },
        };

        var changed = BuiltInProfileSynchronizer.MergeMissingFromTemplate(
            existing,
            BuiltInProfiles.CreateAudioMp3());

        Assert.True(changed);
        Assert.True(existing.Options.ContainsKey("-x"));
        Assert.Equal("mp3", existing.Options["--audio-format"]?.ToString());
    }

    [Fact]
    public void MergeMissingFromTemplate_DoesNotOverwriteCustomFormat()
    {
        var existing = new DownloadProfile
        {
            Id = AppPaths.DefaultProfileId,
            Name = "Default",
            Options = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["-f"] = "custom-format",
            },
        };

        var changed = BuiltInProfileSynchronizer.MergeMissingFromTemplate(
            existing,
            BuiltInProfiles.CreateDefault());

        Assert.True(changed);
        Assert.Equal("custom-format", existing.Options["-f"]?.ToString());
    }
}
