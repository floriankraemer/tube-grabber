// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using Tmds.DBus.Protocol;
using YtDlpUi.Core.Abstractions;

namespace YtDlpUi.UI.Services.Notifications;

public sealed class FreeDesktopNotifier : IDesktopNotifier
{
    private const string Service = "org.freedesktop.Notifications";
    private const string AppName = "Yt-dlp-ui";
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    public async Task ShowAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CallTimeout);
            using var connection = new Connection(Address.Session
                ?? throw new InvalidOperationException("No D-Bus session bus address."));
            await connection.ConnectAsync().AsTask().WaitAsync(timeout.Token).ConfigureAwait(false);

            var message = BuildNotifyMessage(connection, title, EscapeMarkup(body), AppIconFile.EnsureDefault());
            await connection.CallMethodAsync(message).WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Freedesktop notification failed: " + ex);
        }
    }

    internal static string EscapeMarkup(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static MessageBuffer BuildNotifyMessage(Connection connection, string title, string body, string iconPath)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: Service,
            path: "/org/freedesktop/Notifications",
            @interface: Service,
            member: "Notify",
            signature: "susssasa{sv}i");
        writer.WriteString(AppName);
        writer.WriteUInt32(0);
        writer.WriteString(iconPath);
        writer.WriteString(title);
        writer.WriteString(body);
        writer.WriteArray(Array.Empty<string>());
        writer.WriteDictionary(new Dictionary<string, VariantValue>());
        writer.WriteInt32(-1);

        return writer.CreateMessage();
    }
}
