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
        using var guard = SingleInstanceGuard.CreateDefault(App.Services.ConfigRoot);
        if (!guard.TryAcquire())
        {
            guard.NotifyExistingInstance();
            return;
        }

        App.InstanceGuard = guard;
        App.StartMinimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
