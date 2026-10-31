// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

public static class ConfigFilePermissions
{
    public static void ApplyRestrictedPermissions(string path, bool isDirectory)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try
        {
            var mode = isDirectory
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite;

            File.SetUnixFileMode(path, mode);
        }
        catch
        {
            // Best effort on platforms that support it.
        }
    }
}
