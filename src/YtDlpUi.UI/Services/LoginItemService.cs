// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace YtDlpUi.UI.Services;

public sealed class LoginItemService : ILoginItemService
{
    private const string AppName = "Yt-dlp-ui";
    private const string LaunchAgentLabel = "com.github.floriankraemer.ytdlpui";
    private const string MinimizedArgument = "--minimized";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _homeDirectory;
    private readonly string _configHome;
    private readonly string _executablePath;

    public LoginItemService(
        string? homeDirectory = null,
        string? xdgConfigHome = null,
        string? executablePath = null)
    {
        _homeDirectory = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var xdg = xdgConfigHome ?? Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        _configHome = string.IsNullOrWhiteSpace(xdg) ? Path.Combine(_homeDirectory, ".config") : xdg;

        _executablePath = executablePath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot configure start at login: the process path is unknown.");
    }

    public void SetEnabled(bool enabled)
    {
        if (OperatingSystem.IsWindows())
            SetWindows(enabled);
        else if (OperatingSystem.IsMacOS())
            SetMacOs(enabled);
        else
            SetLinux(enabled);
    }

    public bool IsEnabled()
    {
        if (OperatingSystem.IsWindows())
            return IsWindowsEnabled();

        return OperatingSystem.IsMacOS() ? IsMacOsEnabled() : IsLinuxEnabled();
    }

    internal void SetLinux(bool enabled)
    {
        var path = LinuxAutostartPath;
        if (!enabled)
        {
            DeleteIfExists(path);
            return;
        }

        Directory.CreateDirectory(Path.Combine(_configHome, "autostart"));
        File.WriteAllText(path, BuildDesktopEntry());
    }

    internal bool IsLinuxEnabled() => File.Exists(LinuxAutostartPath);

    internal void SetMacOs(bool enabled)
    {
        var path = MacOsLaunchAgentPath;
        if (!enabled)
        {
            DeleteIfExists(path);
            return;
        }

        Directory.CreateDirectory(Path.Combine(_homeDirectory, "Library", "LaunchAgents"));
        File.WriteAllText(path, BuildLaunchAgent());
    }

    internal bool IsMacOsEnabled() => File.Exists(MacOsLaunchAgentPath);

    [SupportedOSPlatform("windows")]
    private void SetWindows(bool enabled)
    {
        if (enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            runKey.SetValue(AppName, $"\"{_executablePath}\" {MinimizedArgument}");
            return;
        }

        using var existingKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        existingKey?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return runKey?.GetValue(AppName) is string;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private string LinuxAutostartPath => Path.Combine(_configHome, "autostart", "yt-dlp-ui.desktop");

    private string MacOsLaunchAgentPath =>
        Path.Combine(_homeDirectory, "Library", "LaunchAgents", $"{LaunchAgentLabel}.plist");

    private string BuildDesktopEntry()
    {
        // Exec quoting escapes shell-reserved characters; the desktop-entry string value then escapes each backslash.
        var exec = $"{QuoteExecArgument(_executablePath)} {MinimizedArgument}".Replace("\\", "\\\\");

        return "[Desktop Entry]\n"
            + "Type=Application\n"
            + $"Name={AppName}\n"
            + $"Exec={exec}\n"
            + "X-GNOME-Autostart-enabled=true\n"
            + "Terminal=false\n";
    }

    private static string QuoteExecArgument(string value) =>
        "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("`", "\\`")
            .Replace("$", "\\$") + "\"";

    private string BuildLaunchAgent()
    {
        var executable = SecurityElement.Escape(_executablePath);

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{LaunchAgentLabel}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{executable}</string>
                    <string>{MinimizedArgument}</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
            </dict>
            </plist>
            """ + "\n";
    }
}
