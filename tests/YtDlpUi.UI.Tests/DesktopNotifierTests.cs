// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.UI.Services.Notifications;

namespace YtDlpUi.UI.Tests;

public sealed class DesktopNotifierTests
{
    [Fact]
    public void Factory_PicksNotifierForCurrentOs()
    {
        var notifier = DesktopNotifierFactory.Create();

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<WindowsToastNotifier>(notifier);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.IsType<FreeDesktopNotifier>(notifier);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.IsType<MacOsNotifier>(notifier);
        }
        else
        {
            Assert.IsType<NullDesktopNotifier>(notifier);
        }
    }

    [Fact]
    public void FreeDesktopNotifier_EscapeMarkup_EscapesReservedCharacters()
    {
        Assert.Equal("a &amp; b &lt;i&gt;", FreeDesktopNotifier.EscapeMarkup("a & b <i>"));
    }

    [Fact]
    public async Task NullDesktopNotifier_CompletesWithoutThrowing()
    {
        await new NullDesktopNotifier().ShowAsync("title", "body", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FreeDesktopNotifier_WithoutSessionBus_DoesNotThrow()
    {
        const string variable = "DBUS_SESSION_BUS_ADDRESS";
        var original = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "unix:path=/nonexistent/yt-dlp-ui-test-bus");
            await new FreeDesktopNotifier().ShowAsync("title", "body", TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }
}
