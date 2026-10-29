// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Constants;

public static class PlatformBinaryAssets
{
    // yt-dlp and Deno publish no "latest" tag, so the latest-release redirect is the stable URL.
    private const string YtDlpDownloadBase = "https://github.com/yt-dlp/yt-dlp/releases/latest/download";
    private const string DenoDownloadBase = "https://github.com/denoland/deno/releases/latest/download";

    // FFmpeg-Builds publishes a rolling release tagged "latest".
    private const string FfmpegDownloadBase = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest";

    public static ReleaseAsset GetYtDlpAsset(string runtimeIdentifier)
    {
        var fileName = ResolveYtDlpFileName(runtimeIdentifier);
        return CreateAsset(
            BinaryReleaseManifest.BuildDownloadUrl(YtDlpDownloadBase, fileName),
            fileName,
            BinaryReleaseManifest.YtDlpReleasePageUrl,
            minimumSizeBytes: 100_000);
    }

    public static ReleaseAsset GetFfmpegAsset(string runtimeIdentifier)
    {
        var fileName = ResolveFfmpegFileName(runtimeIdentifier);
        return CreateAsset(
            BinaryReleaseManifest.BuildDownloadUrl(FfmpegDownloadBase, fileName),
            fileName,
            BinaryReleaseManifest.FfmpegReleasePageUrl,
            minimumSizeBytes: 1_000_000);
    }

    public static ReleaseAsset GetDenoAsset(string runtimeIdentifier)
    {
        var fileName = ResolveDenoFileName(runtimeIdentifier);
        return CreateAsset(
            BinaryReleaseManifest.BuildDownloadUrl(DenoDownloadBase, fileName),
            fileName,
            BinaryReleaseManifest.DenoReleasePageUrl,
            minimumSizeBytes: 1_000_000);
    }

    private static ReleaseAsset CreateAsset(
        string downloadUrl,
        string fileName,
        string releasePageUrl,
        long minimumSizeBytes) =>
        new()
        {
            DownloadUrl = downloadUrl,
            FileName = fileName,
            ReleasePageUrl = releasePageUrl,
            MinimumSizeBytes = minimumSizeBytes,
        };

    private static string ResolveYtDlpFileName(string runtimeIdentifier) =>
        NormalizeRuntimeIdentifier(runtimeIdentifier) switch
        {
            "win-x64" or "win7-x64" => "yt-dlp.exe",
            "win-x86" or "win7-x86" => "yt-dlp_x86.exe",
            "win-arm64" => "yt-dlp_arm64.exe",
            "linux-x64" => "yt-dlp_linux",
            "linux-arm64" => "yt-dlp_linux_aarch64",
            "osx-x64" or "osx-arm64" => "yt-dlp_macos",
            _ => throw CreateUnsupportedException(runtimeIdentifier, "yt-dlp", BinaryReleaseManifest.YtDlpReleasePageUrl),
        };

    private static string ResolveFfmpegFileName(string runtimeIdentifier) =>
        NormalizeRuntimeIdentifier(runtimeIdentifier) switch
        {
            "win-x64" or "win7-x64" =>
                "ffmpeg-master-latest-win64-gpl.zip",
            "win-arm64" =>
                "ffmpeg-master-latest-winarm64-gpl.zip",
            "win-x86" or "win7-x86" =>
                "ffmpeg-master-latest-win32-gpl.zip",
            "linux-x64" =>
                "ffmpeg-master-latest-linux64-gpl.tar.xz",
            "linux-arm64" =>
                "ffmpeg-master-latest-linuxarm64-gpl.tar.xz",
            // FFmpeg-Builds publishes no macOS build, so macOS is reported as unsupported.
            _ => throw CreateUnsupportedException(runtimeIdentifier, "ffmpeg", BinaryReleaseManifest.FfmpegReleasePageUrl),
        };

    private static string ResolveDenoFileName(string runtimeIdentifier) =>
        NormalizeRuntimeIdentifier(runtimeIdentifier) switch
        {
            "win-x64" or "win7-x64" => "deno-x86_64-pc-windows-msvc.zip",
            "win-arm64" => "deno-aarch64-pc-windows-msvc.zip",
            "linux-x64" => "deno-x86_64-unknown-linux-gnu.zip",
            "linux-arm64" => "deno-aarch64-unknown-linux-gnu.zip",
            "osx-x64" => "deno-x86_64-apple-darwin.zip",
            "osx-arm64" => "deno-aarch64-apple-darwin.zip",
            _ => throw CreateUnsupportedException(runtimeIdentifier, "Deno", BinaryReleaseManifest.DenoReleasePageUrl),
        };

    private static string NormalizeRuntimeIdentifier(string runtimeIdentifier) =>
        runtimeIdentifier.Trim().ToLowerInvariant();

    private static UnsupportedPlatformException CreateUnsupportedException(
        string runtimeIdentifier,
        string binaryName,
        string releasePageUrl) =>
        new(
            $"Automatic install of {binaryName} is not supported on {runtimeIdentifier}.",
            runtimeIdentifier,
            binaryName,
            releasePageUrl);
}

public sealed class UnsupportedPlatformException : Exception
{
    public UnsupportedPlatformException(
        string message,
        string runtimeIdentifier,
        string binaryName,
        string releasePageUrl)
        : base(message)
    {
        RuntimeIdentifier = runtimeIdentifier;
        BinaryName = binaryName;
        ReleasePageUrl = releasePageUrl;
    }

    public string RuntimeIdentifier { get; }
    public string BinaryName { get; }
    public string ReleasePageUrl { get; }
}
