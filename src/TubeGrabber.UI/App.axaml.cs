// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.Views;

namespace TubeGrabber.UI;

public sealed partial class App : Application
{
    private static readonly Uri IconUri = new("avares://TubeGrabber.UI/Assets/app-icon.png");

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _mainWindow;
    private TrayIcon? _tray;

    public static AppServices Services { get; } = new();

    internal static SingleInstanceGuard? InstanceGuard { get; set; }

    internal static bool StartMinimized { get; set; }

    internal static bool IsQuitting { get; private set; }

    internal static bool TrayAvailable { get; private set; }

    internal static bool RunInBackground { get; private set; } = true;

    internal static WindowIcon LoadWindowIcon() => new(AssetLoader.Open(IconUri));

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Everything below reads the config; loading it once up front avoids concurrent first-run bootstrap
            // writes and guarantees the preferences are known before the window can be closed.
            var config = Task.Run(LoadStartupConfigAsync).GetAwaiter().GetResult();
            if (config is not null)
            {
                RunInBackground = config.RunInBackground;
                ThemeService.Apply(config.ThemePreference);
            }

            MigrateLegacyLoginItem();

            _mainWindow = new MainWindow
            {
                DataContext = Services.CreateMainViewModel(),
            };
            TrayAvailable = TryCreateTray() && TrayHost.IsAvailable();

            if (TrayHost.ShouldShowWindowOnStart(StartMinimized, TrayAvailable))
                desktop.MainWindow = _mainWindow;

            InstanceGuard?.StartListening(() => Dispatcher.UIThread.Post(ShowMainWindow));
        }

        base.OnFrameworkInitializationCompleted();
        Services.SubscriptionMonitor.Start();
    }

    private static void MigrateLegacyLoginItem()
    {
        try
        {
            Services.LoginItemService.MigrateLegacyEntry();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to migrate the legacy start-at-login entry: {ex}");
        }
    }

    internal static async Task RefreshRunInBackgroundAsync()
    {
        try
        {
            var config = await Services.AppConfigStore.LoadAsync();
            RunInBackground = config.RunInBackground;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to load run-in-background preference: {ex}");
        }
    }

    internal static async Task QuitAsync()
    {
        if (IsQuitting)
            return;

        IsQuitting = true;
        try
        {
            if (Current is App { _mainWindow: { } window })
            {
                await window.SaveStateAsync();
                window.DisposePages();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to save window state: {ex}");
        }

        try
        {
            await Services.SubscriptionMonitor.DisposeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to stop subscription monitor: {ex}");
        }

        var app = (App)Current!;
        app._tray?.Dispose();
        app._desktop?.Shutdown();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is not { } window)
            return;

        if (!window.IsVisible)
            window.Show();

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
    }

    private void ShowSubscriptionsPage()
    {
        ShowMainWindow();
        _mainWindow?.OpenSubscriptionsPage();
    }

    private bool TryCreateTray()
    {
        try
        {
            var open = new NativeMenuItem("Open Tube Grabber");
            open.Click += (_, _) => ShowMainWindow();
            var subscriptions = new NativeMenuItem("Subscriptions…");
            subscriptions.Click += (_, _) => ShowSubscriptionsPage();
            var check = new NativeMenuItem("Check subscriptions now");
            check.Click += async (_, _) => await CheckSubscriptionsNowAsync();
            var quit = new NativeMenuItem("Quit");
            quit.Click += async (_, _) => await QuitAsync();

            _tray = new TrayIcon
            {
                Icon = LoadWindowIcon(),
                ToolTipText = "Tube Grabber",
                Menu = new NativeMenu { open, subscriptions, check, new NativeMenuItemSeparator(), quit },
            };
            _tray.Clicked += (_, _) => ShowMainWindow();
            TrayIcon.SetIcons(this, new TrayIcons { _tray });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to create tray icon: {ex}");
            return false;
        }
    }

    private static async Task CheckSubscriptionsNowAsync()
    {
        try
        {
            await Services.SubscriptionMonitor.CheckNowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Subscription check failed: {ex}");
        }
    }

    private static async Task<AppConfiguration?> LoadStartupConfigAsync()
    {
        try
        {
            return await Services.AppConfigStore.LoadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to load startup configuration: {ex}");
            return null;
        }
    }
}
