// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class BinaryLocator : IBinaryLocator
{
    private readonly string _configRoot;

    public BinaryLocator(string configRoot) => _configRoot = configRoot;

    public string? ResolveYtDlpPath(AppConfiguration config)
    {
        var configured = ExecutablePathResolver.NormalizeIfExists(config.YtDlpPath);
        if (configured is not null)
            return configured;

        var bundled = GetBundledYtDlpPath();
        if (File.Exists(bundled))
            return bundled;

        return ExecutablePathResolver.FindOnPath(GetYtDlpExecutableName());
    }

    public string? ResolveFfmpegPath(AppConfiguration config)
    {
        var configured = ExecutablePathResolver.NormalizeIfExists(config.FfmpegPath);
        if (configured is not null)
            return configured;

        var bundled = GetBundledFfmpegPath();
        if (File.Exists(bundled))
            return bundled;

        return ExecutablePathResolver.FindOnPath(GetFfmpegExecutableName());
    }

    public string GetBundledYtDlpPath() =>
        Path.Combine(_configRoot, AppPaths.BinFolderName, "yt-dlp", GetYtDlpExecutableName());

    public string GetBundledFfmpegPath() =>
        Path.Combine(_configRoot, AppPaths.BinFolderName, "ffmpeg", GetFfmpegExecutableName());

    public string GetBundledDenoPath() =>
        Path.Combine(_configRoot, AppPaths.BinFolderName, "deno", GetDenoExecutableName());

    private static string GetYtDlpExecutableName() =>
        OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

    private static string GetFfmpegExecutableName() =>
        OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    private static string GetDenoExecutableName() =>
        OperatingSystem.IsWindows() ? "deno.exe" : "deno";
}
