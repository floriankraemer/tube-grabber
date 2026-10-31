// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class LoginItemServiceTests : IDisposable
{
    private const string ExecutablePath = "/opt/tube-grabber/TubeGrabber";
    private const string IconPath = "/home/u/.tube-grabber/app-icon.png";

    private readonly string _root = Directory.CreateTempSubdirectory("login-item-tests-").FullName;

    private string HomeDirectory => Path.Combine(_root, "home");

    private string XdgConfigHome => Path.Combine(_root, "xdg");

    private string DesktopEntryPath => Path.Combine(XdgConfigHome, "autostart", "tube-grabber.desktop");

    private string LaunchAgentPath =>
        Path.Combine(HomeDirectory, "Library", "LaunchAgents", "com.github.floriankraemer.tubegrabber.plist");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private LoginItemService CreateService(string executablePath = ExecutablePath, string iconPath = IconPath) =>
        new(HomeDirectory, XdgConfigHome, executablePath, iconPath);

    [Fact]
    public void MigrateLegacyEntry_OnLinuxWithLegacyEntry_ReplacesItWithCurrentEntry()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var legacyPath = Path.Combine(XdgConfigHome, "autostart", "yt-dlp-ui.desktop");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, "[Desktop Entry]\nExec=/old/YtDlpUi\n");

        CreateService().MigrateLegacyEntry();

        Assert.False(File.Exists(legacyPath));
        Assert.Contains("Exec=\"/opt/tube-grabber/TubeGrabber\" --minimized", File.ReadAllText(DesktopEntryPath));
    }

    [Fact]
    public void MigrateLegacyEntry_OnLinuxWithoutLegacyEntry_DoesNotEnableAutostart()
    {
        if (!OperatingSystem.IsLinux())
            return;

        CreateService().MigrateLegacyEntry();

        Assert.False(File.Exists(DesktopEntryPath));
    }

    [Fact]
    public void RemoveLegacyMacOs_DeletesOldLaunchAgent()
    {
        var legacyPath = Path.Combine(HomeDirectory, "Library", "LaunchAgents", "com.github.floriankraemer.ytdlpui.plist");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, "<plist/>");
        var service = CreateService();

        Assert.True(service.RemoveLegacyMacOs());
        Assert.False(File.Exists(legacyPath));
        Assert.False(service.RemoveLegacyMacOs());
    }

    [Fact]
    public void SetLinux_WhenEnabled_WritesDesktopEntry()
    {
        CreateService().SetLinux(enabled: true);

        Assert.Equal(
            "[Desktop Entry]\n"
            + "Type=Application\n"
            + "Name=Tube Grabber\n"
            + "Comment=Download queue and channel subscriptions for yt-dlp\n"
            + "Exec=\"/opt/tube-grabber/TubeGrabber\" --minimized\n"
            + "Icon=/home/u/.tube-grabber/app-icon.png\n"
            + "StartupWMClass=TubeGrabber\n"
            + "X-GNOME-Autostart-enabled=true\n"
            + "Terminal=false\n",
            File.ReadAllText(DesktopEntryPath));
    }

    [Fact]
    public void SetLinux_EscapesPercentInExec()
    {
        CreateService("/opt/100%/TubeGrabber").SetLinux(enabled: true);

        Assert.Contains("Exec=\"/opt/100%%/TubeGrabber\" --minimized", File.ReadAllText(DesktopEntryPath));
    }

    [Fact]
    public void SetLinux_WithoutIcon_OmitsIconLine()
    {
        CreateService(iconPath: string.Empty).SetLinux(enabled: true);

        Assert.DoesNotContain("Icon=", File.ReadAllText(DesktopEntryPath));
    }

    [Fact]
    public void SetLinux_WithoutXdgConfigHome_UsesHomeConfigDirectory()
    {
        var service = new LoginItemService(HomeDirectory, xdgConfigHome: "", ExecutablePath);

        service.SetLinux(enabled: true);

        Assert.True(File.Exists(Path.Combine(HomeDirectory, ".config", "autostart", "tube-grabber.desktop")));
    }

    [Fact]
    public void SetLinux_EscapesReservedCharactersInExecPath()
    {
        const string path = "/opt/a\"b$c`d\\e";

        CreateService(path).SetLinux(enabled: true);

        Assert.Contains(
            @"Exec=""/opt/a\\""b\\$c\\`d\\\\e"" --minimized",
            File.ReadAllText(DesktopEntryPath));
    }

    [Fact]
    public void SetLinux_WhenDisabled_RemovesDesktopEntry()
    {
        var service = CreateService();
        service.SetLinux(enabled: true);

        service.SetLinux(enabled: false);

        Assert.False(File.Exists(DesktopEntryPath));
    }

    [Fact]
    public void SetLinux_WhenDisabledAndMissing_DoesNotThrow()
    {
        var service = CreateService();

        service.SetLinux(enabled: false);

        Assert.False(File.Exists(DesktopEntryPath));
    }

    [Fact]
    public void IsLinuxEnabled_ReflectsDesktopEntryPresence()
    {
        var service = CreateService();

        Assert.False(service.IsLinuxEnabled());
        service.SetLinux(enabled: true);
        Assert.True(service.IsLinuxEnabled());
        service.SetLinux(enabled: false);
        Assert.False(service.IsLinuxEnabled());
    }

    [Fact]
    public void IsEnabled_OnLinux_ReflectsDesktopEntryPresence()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var service = CreateService();

        service.SetEnabled(enabled: true);
        Assert.True(service.IsEnabled());
        service.SetEnabled(enabled: false);
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetMacOs_WhenEnabled_WritesLaunchAgent()
    {
        CreateService().SetMacOs(enabled: true);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
            + "<plist version=\"1.0\">\n"
            + "<dict>\n"
            + "    <key>Label</key>\n"
            + "    <string>com.github.floriankraemer.tubegrabber</string>\n"
            + "    <key>ProgramArguments</key>\n"
            + "    <array>\n"
            + "        <string>/opt/tube-grabber/TubeGrabber</string>\n"
            + "        <string>--minimized</string>\n"
            + "    </array>\n"
            + "    <key>RunAtLoad</key>\n"
            + "    <true/>\n"
            + "</dict>\n"
            + "</plist>\n",
            File.ReadAllText(LaunchAgentPath));
    }

    [Fact]
    public void SetMacOs_XmlEscapesExecutablePath()
    {
        CreateService("/Users/a&b/<x>'\"").SetMacOs(enabled: true);

        Assert.Contains(
            "<string>/Users/a&amp;b/&lt;x&gt;&apos;&quot;</string>",
            File.ReadAllText(LaunchAgentPath));
    }

    [Fact]
    public void SetMacOs_WhenDisabled_RemovesLaunchAgent()
    {
        var service = CreateService();
        service.SetMacOs(enabled: true);
        Assert.True(service.IsMacOsEnabled());

        service.SetMacOs(enabled: false);

        Assert.False(File.Exists(LaunchAgentPath));
        Assert.False(service.IsMacOsEnabled());
    }
}
