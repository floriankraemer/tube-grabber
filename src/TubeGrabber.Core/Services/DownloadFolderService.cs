// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class DownloadFolderService
{
    public static string GetSuggestedDefaultPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        return Path.Combine(home, AppPaths.DefaultDownloadFolderName);
    }

    public bool IsConfigured(AppConfiguration config) =>
        TryNormalize(config.DownloadFolder, out _);

    public bool TryNormalize(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            normalizedPath = Path.GetFullPath(path.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool EnsureExists(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string ResolveWorkingDirectory(AppConfiguration config, DownloadProfile profile)
    {
        if (profile.Options.TryGetValue("-P", out var pathValue))
        {
            var pathText = ProfileOptionReader.ReadString(pathValue);
            if (!string.IsNullOrWhiteSpace(pathText) && TryNormalize(pathText, out var profilePath))
            {
                EnsureExists(profilePath);
                return profilePath;
            }
        }

        if (!TryNormalize(config.DownloadFolder, out var downloadFolder))
            throw new InvalidOperationException(
                "Download folder is not configured. Open Settings → Queue and set a download folder.");

        EnsureExists(downloadFolder);
        return downloadFolder;
    }
}
