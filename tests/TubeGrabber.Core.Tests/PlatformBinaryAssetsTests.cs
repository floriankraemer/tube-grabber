// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;

namespace TubeGrabber.Core.Tests;

public sealed class PlatformBinaryAssetsTests
{
    private const string YtDlpDownloadBase = "https://github.com/yt-dlp/yt-dlp/releases/latest/download";
    private const string FfmpegDownloadBase = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest";
    private const string DenoDownloadBase = "https://github.com/denoland/deno/releases/latest/download";

    [Theory]
    [InlineData("win-x64", "yt-dlp.exe")]
    [InlineData("win7-x64", "yt-dlp.exe")]
    [InlineData("win-x86", "yt-dlp_x86.exe")]
    [InlineData("win-arm64", "yt-dlp_arm64.exe")]
    [InlineData("linux-x64", "yt-dlp_linux")]
    [InlineData("linux-arm64", "yt-dlp_linux_aarch64")]
    [InlineData("osx-x64", "yt-dlp_macos")]
    [InlineData("osx-arm64", "yt-dlp_macos")]
    public void GetYtDlpAsset_UsesPlatformSpecificFileName(string rid, string expectedFileName)
    {
        var asset = PlatformBinaryAssets.GetYtDlpAsset(rid);

        Assert.Equal(expectedFileName, asset.FileName);
        Assert.Equal($"{YtDlpDownloadBase}/{expectedFileName}", asset.DownloadUrl);
        Assert.Equal(BinaryReleaseManifest.YtDlpReleasePageUrl, asset.ReleasePageUrl);
    }

    [Theory]
    [InlineData("win-x64", "ffmpeg-master-latest-win64-gpl.zip")]
    [InlineData("win7-x64", "ffmpeg-master-latest-win64-gpl.zip")]
    [InlineData("win-arm64", "ffmpeg-master-latest-winarm64-gpl.zip")]
    [InlineData("win-x86", "ffmpeg-master-latest-win32-gpl.zip")]
    [InlineData("linux-x64", "ffmpeg-master-latest-linux64-gpl.tar.xz")]
    [InlineData("linux-arm64", "ffmpeg-master-latest-linuxarm64-gpl.tar.xz")]
    public void GetFfmpegAsset_UsesPlatformSpecificFileName(string rid, string expectedFileName)
    {
        var asset = PlatformBinaryAssets.GetFfmpegAsset(rid);

        Assert.Equal(expectedFileName, asset.FileName);
        Assert.Equal($"{FfmpegDownloadBase}/{expectedFileName}", asset.DownloadUrl);
        Assert.Equal(BinaryReleaseManifest.FfmpegReleasePageUrl, asset.ReleasePageUrl);
    }

    [Theory]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void GetFfmpegAsset_MacOs_IsUnsupported(string rid)
    {
        var ex = Assert.Throws<UnsupportedPlatformException>(() => PlatformBinaryAssets.GetFfmpegAsset(rid));
        Assert.Equal(BinaryReleaseManifest.FfmpegReleasePageUrl, ex.ReleasePageUrl);
    }

    [Theory]
    [InlineData("win-x64", "deno-x86_64-pc-windows-msvc.zip")]
    [InlineData("win-arm64", "deno-aarch64-pc-windows-msvc.zip")]
    [InlineData("linux-x64", "deno-x86_64-unknown-linux-gnu.zip")]
    [InlineData("linux-arm64", "deno-aarch64-unknown-linux-gnu.zip")]
    [InlineData("osx-x64", "deno-x86_64-apple-darwin.zip")]
    [InlineData("osx-arm64", "deno-aarch64-apple-darwin.zip")]
    public void GetDenoAsset_UsesPlatformSpecificFileName(string rid, string expectedFileName)
    {
        var asset = PlatformBinaryAssets.GetDenoAsset(rid);

        Assert.Equal(expectedFileName, asset.FileName);
        Assert.Equal($"{DenoDownloadBase}/{expectedFileName}", asset.DownloadUrl);
    }

    [Theory]
    [InlineData("linux-musl-x64")]
    [InlineData("freebsd-x64")]
    public void GetYtDlpAsset_UnsupportedPlatform_Throws(string rid)
    {
        var ex = Assert.Throws<UnsupportedPlatformException>(() => PlatformBinaryAssets.GetYtDlpAsset(rid));
        Assert.Equal(BinaryReleaseManifest.YtDlpReleasePageUrl, ex.ReleasePageUrl);
    }
}
