// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

public static class ExecutablePathResolver
{
    public static string? NormalizeIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var fullPath = Path.GetFullPath(path.Trim());
        return File.Exists(fullPath) ? fullPath : null;
    }

    public static string? FindOnPath(string executable, bool includeWindowsExe = true)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        foreach (var folder in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim(), executable);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            if (includeWindowsExe && OperatingSystem.IsWindows())
            {
                var withExtension = candidate + ".exe";
                if (File.Exists(withExtension))
                    return Path.GetFullPath(withExtension);
            }
        }

        return null;
    }
}
