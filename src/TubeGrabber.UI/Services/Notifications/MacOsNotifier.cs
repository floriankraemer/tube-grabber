// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using TubeGrabber.Core.Abstractions;

namespace TubeGrabber.UI.Services.Notifications;

public sealed class MacOsNotifier : IDesktopNotifier
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task ShowAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo("osascript")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[]
                     {
                         "-e", "on run argv",
                         "-e", "display notification (item 2 of argv) with title (item 1 of argv)",
                         "-e", "end run",
                         title, body,
                     })
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                throw;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("macOS notification failed: " + ex);
        }
    }
}
