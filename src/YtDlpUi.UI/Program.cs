// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using YtDlpUi.UI.Services;

namespace YtDlpUi.UI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var startMinimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        using var guard = SingleInstanceGuard.CreateDefault(App.Services.ConfigRoot);
        if (!guard.TryAcquire())
        {
            // An autostart launch must not pop up the window of the instance that is already running.
            if (!startMinimized)
                guard.NotifyExistingInstance();
            return;
        }

        App.InstanceGuard = guard;
        App.StartMinimized = startMinimized;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
