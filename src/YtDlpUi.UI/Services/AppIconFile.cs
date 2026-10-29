// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using Avalonia.Platform;
using YtDlpUi.Core.Constants;

namespace YtDlpUi.UI.Services;

internal static class AppIconFile
{
    private const string FileName = "app-icon.png";
    private static readonly Uri IconUri = new("avares://YtDlpUi.UI/Assets/app-icon.png");

    public static string EnsureDefault() =>
        EnsureWritten(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppPaths.ConfigFolderName),
            () => AssetLoader.Open(IconUri));

    // Returns an empty string when the icon cannot be written; callers then omit the icon.
    internal static string EnsureWritten(string directory, Func<Stream> openIcon)
    {
        var path = Path.Combine(directory, FileName);
        try
        {
            if (File.Exists(path))
                return path;

            Directory.CreateDirectory(directory);
            using var source = openIcon();
            using var target = File.Create(path);
            source.CopyTo(target);
            return path;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not write app icon file: " + ex.Message);
            return string.Empty;
        }
    }
}
