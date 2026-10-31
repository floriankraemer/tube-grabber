// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class ProfileFfmpegRequirement
{
    private static readonly ExtraArgsTokenizer ExtraArgsTokenizer = new();

    public const string FfmpegRequiredMessage =
        "ffmpeg is required for this profile (audio/video conversion). Install ffmpeg or set its path in Settings → Binaries.";

    public static bool RequiresFfmpeg(DownloadProfile profile)
    {
        if (ProfileHasTruthyOption(profile, "-x"))
            return true;

        if (ProfileHasNonEmptyStringOption(profile, "--audio-format"))
            return true;

        return ContainsToken(profile.ExtraArgs, "-x")
            || ContainsToken(profile.ExtraArgs, "--audio-format");
    }

    private static bool ProfileHasTruthyOption(DownloadProfile profile, string flag) =>
        profile.Options.TryGetValue(flag, out var value) && ProfileOptionReader.IsTruthy(value);

    private static bool ProfileHasNonEmptyStringOption(DownloadProfile profile, string flag) =>
        profile.Options.TryGetValue(flag, out var value)
        && !string.IsNullOrWhiteSpace(ProfileOptionReader.ReadString(value));

    private static bool ContainsToken(string extraArgs, string token)
    {
        if (string.IsNullOrWhiteSpace(extraArgs))
            return false;

        return ExtraArgsTokenizer.Tokenize(extraArgs)
            .Any(part => string.Equals(part, token, StringComparison.Ordinal));
    }
}
