// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Constants;

public static class BinaryReleaseManifest
{
    public const string LatestReleaseTag = "latest";

    public const string YtDlpReleaseTag = LatestReleaseTag;
    public const string FfmpegBuildId = LatestReleaseTag;

    public const string YtDlpReleasePageUrl = "https://github.com/yt-dlp/yt-dlp/releases";
    public const string FfmpegReleasePageUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases";
    public const string DenoReleasePageUrl = "https://github.com/denoland/deno/releases";

    public static string BuildDownloadUrl(string downloadBase, string fileName) =>
        $"{downloadBase}/{fileName}";
}
