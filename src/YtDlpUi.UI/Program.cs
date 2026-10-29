// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using Avalonia;
using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Services;
using YtDlpUi.UI.Services;

namespace YtDlpUi.UI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        InstallFileLogging();
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

    private static void InstallFileLogging()
    {
        var listener = new RollingFileTraceListener(Path.Combine(App.Services.ConfigRoot, AppPaths.LogsFolderName));
        Trace.Listeners.Add(listener);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            listener.LogException("UnhandledException", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
            listener.Flush();
        };
        TaskScheduler.UnobservedTaskException += (_, e) => listener.LogException("UnobservedTaskException", e.Exception);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
