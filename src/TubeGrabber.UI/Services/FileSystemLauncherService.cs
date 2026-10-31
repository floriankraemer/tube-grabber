// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;

namespace TubeGrabber.UI.Services;

public sealed class FileSystemLauncherService : IFileSystemLauncher
{
    public bool TryOpenFile(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            }

            if (OperatingSystem.IsMacOS())
                return StartProcess("open", path);

            return StartProcess("xdg-open", path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool TryOpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }

            if (OperatingSystem.IsMacOS())
                return StartProcess("open", url);

            return StartProcess("xdg-open", url);
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool TryOpenLocation(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                if (OperatingSystem.IsWindows())
                    return StartProcess("explorer.exe", $"/select,\"{path}\"");

                if (OperatingSystem.IsMacOS())
                    return StartProcess("open", $"-R \"{path}\"");

                var directory = Path.GetDirectoryName(path);
                return directory is not null && StartProcess("xdg-open", directory);
            }

            if (!Directory.Exists(path))
                return false;

            if (OperatingSystem.IsWindows())
                return StartProcess("explorer.exe", path);

            if (OperatingSystem.IsMacOS())
                return StartProcess("open", path);

            return StartProcess("xdg-open", path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool StartProcess(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
        });

        return process is not null;
    }
}
