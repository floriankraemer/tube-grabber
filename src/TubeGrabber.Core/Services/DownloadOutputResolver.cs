// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public readonly record struct DownloadOutputTarget(bool CanOpen, bool IsSingleFile, string Path);

public static class DownloadOutputResolver
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static DownloadOutputTarget Resolve(DownloadJob job)
    {
        if (job.Status != DownloadStatus.Completed)
            return default;

        var files = GetExistingOutputFiles(job);
        if (files.Count == 1)
            return new DownloadOutputTarget(true, true, files[0]);

        if (files.Count > 1)
        {
            var directory = GetCommonDirectory(files) ?? GetExistingWorkingDirectory(job);
            return directory is not null
                ? new DownloadOutputTarget(true, false, directory)
                : default;
        }

        var workingDirectory = GetExistingWorkingDirectory(job);
        return workingDirectory is not null
            ? new DownloadOutputTarget(true, false, workingDirectory)
            : default;
    }

    public static bool CanOpen(DownloadJob job) => Resolve(job).CanOpen;

    public static bool IsSingleFile(DownloadJob job) => Resolve(job).IsSingleFile;

    private static List<string> GetExistingOutputFiles(DownloadJob job)
    {
        var files = new List<string>();
        foreach (var candidate in job.OutputPaths)
        {
            var resolved = ResolvePath(candidate, job.WorkingDirectory);
            if (resolved is null || !File.Exists(resolved))
                continue;

            if (IsTemporaryPartFile(resolved))
                continue;

            if (!files.Contains(resolved, PathComparer))
                files.Add(resolved);
        }

        return files;
    }

    private static string? GetExistingWorkingDirectory(DownloadJob job)
    {
        var directory = ResolvePath(job.WorkingDirectory, null);
        return directory is not null && Directory.Exists(directory) ? directory : null;
    }

    private static string? ResolvePath(string? path, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var trimmed = path.Trim();
            if (!Path.IsPathRooted(trimmed))
            {
                if (string.IsNullOrWhiteSpace(workingDirectory))
                    return null;

                trimmed = Path.Combine(workingDirectory.Trim(), trimmed);
            }

            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCommonDirectory(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return null;

        var directories = filePaths
            .Select(Path.GetDirectoryName)
            .Where(static directory => !string.IsNullOrWhiteSpace(directory))
            .Select(static directory => directory!)
            .Distinct(PathComparer)
            .ToList();

        return directories.Count == 1 ? directories[0] : null;
    }

    private static bool IsTemporaryPartFile(string path) =>
        path.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase);
}
