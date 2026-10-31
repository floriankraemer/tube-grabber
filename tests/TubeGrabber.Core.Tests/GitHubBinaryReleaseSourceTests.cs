// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class GitHubBinaryReleaseSourceTests
{
    [Theory]
    [InlineData("linux-x64", "yt-dlp_linux")]
    [InlineData("win-x64", "yt-dlp.exe")]
    public void GetYtDlpAsset_UsesExpectedFileName(string rid, string fileName)
    {
        var source = new GitHubBinaryReleaseSource();
        var asset = source.GetYtDlpAsset(rid);
        Assert.Equal(fileName, asset.FileName);
        Assert.Contains(fileName, asset.DownloadUrl);
    }

    [Fact]
    public void GetFfmpegAsset_ReturnsArchiveForLinux()
    {
        var source = new GitHubBinaryReleaseSource();
        var asset = source.GetFfmpegAsset("linux-x64");
        Assert.Contains("ffmpeg", asset.FileName, StringComparison.OrdinalIgnoreCase);
    }
}
