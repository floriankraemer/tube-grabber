// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using YtDlpUi.Core.Abstractions;
#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace YtDlpUi.UI.Services.Notifications;

public sealed class WindowsToastNotifier : IDesktopNotifier
{
    public Task ShowAsync(string title, string body, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        try
        {
            new ToastContentBuilder().AddText(title).AddText(body).Show();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Windows toast notification failed: " + ex);
        }
#else
        Trace.TraceWarning("Windows toast notifications are unavailable in this build.");
#endif
        return Task.CompletedTask;
    }
}
