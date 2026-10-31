// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class BuiltInProfiles
{
    public static bool IsBuiltIn(string profileId) =>
        All.Any(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal));

    public static DownloadProfile? FindTemplate(string profileId) =>
        All.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal));

    public static IReadOnlyList<DownloadProfile> All { get; } =
    [
        CreateDefault(),
        CreateAudioMp3(),
        CreateHqVideo(),
    ];

    public static DownloadProfile CreateDefault() => new()
    {
        Id = AppPaths.DefaultProfileId,
        Name = "Default",
        Options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["-f"] = "b[ext=mp4]/bv*+ba/b",
            ["-o"] = "%(title)s [%(id)s].%(ext)s",
            ["--merge-output-format"] = "mp4",
            ["--force-overwrites"] = true,
            ["--no-part"] = true,
        },
        ExtraArgs = string.Empty,
    };

    /// <summary>
    /// Based on downloadaudio2.ps1: best audio stream, MP3 extract, SponsorBlock, parallel fragments.
    /// </summary>
    public static DownloadProfile CreateAudioMp3() => new()
    {
        Id = AppPaths.AudioMp3ProfileId,
        Name = "Download Audio as mp3",
        Options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["-f"] = "bestaudio",
            ["-x"] = true,
            ["--audio-format"] = "mp3",
            ["--audio-quality"] = "2",
            ["--force-overwrites"] = true,
            ["--concurrent-fragments"] = 16,
            ["--sponsorblock-remove"] = "sponsor,intro,outro,selfpromo,interaction",
            ["--postprocessor-args"] = "ffmpeg:-threads 8",
            ["-o"] = "%(channel)s/%(title)s.%(ext)s",
        },
        ExtraArgs = string.Empty,
    };

    /// <summary>
    /// HQ video as one output file (prefers progressive MP4, otherwise merges to MP4).
    /// </summary>
    public static DownloadProfile CreateHqVideo() => new()
    {
        Id = AppPaths.HqVideoProfileId,
        Name = "Download HQ Video",
        Options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["-f"] = "b[ext=mp4]/bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[ext=mp4]/b",
            ["-S"] = "res:1080,vcodec:h264,acodec:aac",
            ["--merge-output-format"] = "mp4",
            ["-o"] = "%(title)s [%(id)s].%(ext)s",
            ["--force-overwrites"] = true,
            ["--no-part"] = true,
        },
        ExtraArgs = string.Empty,
    };
}
