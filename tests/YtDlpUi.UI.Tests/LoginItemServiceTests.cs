// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.UI.Services;

namespace YtDlpUi.UI.Tests;

public sealed class LoginItemServiceTests : IDisposable
{
    private const string ExecutablePath = "/opt/yt-dlp-ui/YtDlpUi";

    private readonly string _root = Directory.CreateTempSubdirectory("login-item-tests-").FullName;

    private string HomeDirectory => Path.Combine(_root, "home");

    private string XdgConfigHome => Path.Combine(_root, "xdg");

    private string DesktopEntryPath => Path.Combine(XdgConfigHome, "autostart", "yt-dlp-ui.desktop");

    private string LaunchAgentPath =>
        Path.Combine(HomeDirectory, "Library", "LaunchAgents", "com.github.floriankraemer.ytdlpui.plist");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private LoginItemService CreateService(string executablePath = ExecutablePath) =>
        new(HomeDirectory, XdgConfigHome, executablePath);

    [Fact]
    public void SetLinux_WhenEnabled_WritesDesktopEntry()
    {
        CreateService().SetLinux(enabled: true);

        Assert.Equal(
            "[Desktop Entry]\n"
            + "Type=Application\n"
            + "Name=Yt-dlp-ui\n"
            + "Exec=\"/opt/yt-dlp-ui/YtDlpUi\" --minimized\n"
            + "X-GNOME-Autostart-enabled=true\n"
            + "Terminal=false\n",
            File.ReadAllText(DesktopEntryPath));
    }

    [Fact]
    public void SetLinux_WithoutXdgConfigHome_UsesHomeConfigDirectory()
    {
        var service = new LoginItemService(HomeDirectory, xdgConfigHome: "", ExecutablePath);

        service.SetLinux(enabled: true);

        Assert.True(File.Exists(Path.Combine(HomeDirectory, ".config", "autostart", "yt-dlp-ui.desktop")));
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
            + "    <string>com.github.floriankraemer.ytdlpui</string>\n"
            + "    <key>ProgramArguments</key>\n"
            + "    <array>\n"
            + "        <string>/opt/yt-dlp-ui/YtDlpUi</string>\n"
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
