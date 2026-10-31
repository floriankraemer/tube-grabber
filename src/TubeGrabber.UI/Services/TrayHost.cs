// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using Tmds.DBus.Protocol;

namespace TubeGrabber.UI.Services;

/// <summary>Decides how the main window behaves based on whether a tray icon can actually be seen.</summary>
public static class TrayHost
{
    private const string Watcher = "org.kde.StatusNotifierWatcher";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Closing the window only hides it when the user wants that and a tray icon can bring it back.</summary>
    public static bool ShouldHideOnClose(bool runInBackground, bool trayAvailable) => runInBackground && trayAvailable;

    /// <summary>A hidden start without a visible tray icon would leave no way to reach the window.</summary>
    public static bool ShouldShowWindowOnStart(bool startMinimized, bool trayAvailable) => !startMinimized || !trayAvailable;

    /// <summary>Windows and macOS always have a notification area; Linux needs a StatusNotifierItem host.</summary>
    public static bool IsAvailable() =>
        !OperatingSystem.IsLinux() || HasLinuxHost().GetAwaiter().GetResult();

    private static async Task<bool> HasLinuxHost()
    {
        try
        {
            using var timeout = new CancellationTokenSource(ProbeTimeout);
            using var connection = new DBusConnection(DBusAddress.Session
                ?? throw new InvalidOperationException("No D-Bus session bus address."));
            await connection.ConnectAsync().AsTask().WaitAsync(timeout.Token).ConfigureAwait(false);

            var message = BuildNameHasOwnerMessage(connection);

            return await connection
                .CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadBool())
                .WaitAsync(timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Tray host probe failed, assuming no tray: " + ex);
            return false;
        }
    }

    private static MessageBuffer BuildNameHasOwnerMessage(DBusConnection connection)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: "org.freedesktop.DBus",
            path: "/org/freedesktop/DBus",
            @interface: "org.freedesktop.DBus",
            member: "NameHasOwner",
            signature: "s");
        writer.WriteString(Watcher);
        return writer.CreateMessage();
    }
}
