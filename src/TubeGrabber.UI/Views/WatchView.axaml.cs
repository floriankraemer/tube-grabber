// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class WatchView : UserControl
{
    private static readonly Thickness PageMargin = new(32, 0, 32, 24);

    // Used when the platform reports no double-click time.
    private static readonly TimeSpan FallbackDoubleClickInterval = TimeSpan.FromMilliseconds(500);

    // A resize drag fires many size changes; the frame is redrawn once they stop.
    private static readonly TimeSpan ResizeSettleDelay = TimeSpan.FromMilliseconds(150);

    private readonly ClickArbiter _clicks;
    private IDisposable? _redrawTimer;
    private Window? _window;
    private IDisposable? _windowStateSubscription;

    public WatchView()
    {
        InitializeComponent();
        _clicks = new ClickArbiter(
            () => Application.Current?.PlatformSettings?.GetDoubleTapTime(PointerType.Mouse) ?? FallbackDoubleClickInterval,
            (delay, action) => DispatcherTimer.RunOnce(action, delay),
            () => ViewModel?.TogglePlayPause(),
            ToggleFullscreen);
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => ApplyLayout();
        // The native surface swallows nothing once LibVLC input is off, so clicks on it reach Avalonia like on any control.
        VideoHost.SizeChanged += (_, _) =>
        {
            _redrawTimer?.Dispose();
            _redrawTimer = DispatcherTimer.RunOnce(() => ViewModel?.RedrawPausedFrame(), ResizeSettleDelay);
        };
        VideoHost.Tapped += OnVideoTapped;
        VideoHost.DoubleTapped += OnVideoDoubleTapped;
        SeekSlider.AddHandler(PointerPressedEvent, (_, _) => ViewModel?.BeginScrub(), RoutingStrategies.Tunnel);
        SeekSlider.AddHandler(PointerReleasedEvent, (_, _) => ViewModel?.CommitSeek(), RoutingStrategies.Tunnel, handledEventsToo: true);
        SeekSlider.AddHandler(KeyDownEvent, (_, _) => ViewModel?.BeginScrub(), RoutingStrategies.Tunnel);
        SeekSlider.AddHandler(KeyUpEvent, (_, _) => ViewModel?.CommitSeek(), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private WatchViewModel? ViewModel => DataContext as WatchViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        _windowStateSubscription = _window?.GetObservable(Window.WindowStateProperty)
            .Subscribe(new ActionObserver<WindowState>(OnWindowStateChanged));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _windowStateSubscription?.Dispose();
        _clicks.Cancel();
        _redrawTimer?.Dispose();
        // Release the native surface before the view model disposes the player.
        Video.MediaPlayer = null;
        if (_window?.WindowState == WindowState.FullScreen)
            _window.WindowState = WindowState.Normal;
    }

    private async void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            Video.MediaPlayer = null;
            return;
        }

        // The player must be bound to the native surface before playback starts, or VLC opens its own window.
        if (vm.Player is VlcVideoPlayer vlc)
        {
            // The native child window would otherwise take keyboard focus and mouse clicks (Esc, click to pause).
            vlc.MediaPlayer.EnableKeyInput = false;
            vlc.MediaPlayer.EnableMouseInput = false;
            Video.MediaPlayer = vlc.MediaPlayer;
        }

        vm.Profiles.CollectionChanged += (_, _) => RebuildDownloadMenu();
        RebuildDownloadMenu();
        ApplyLayout();
        await vm.LoadAsync();
    }

    private void OnWindowStateChanged(WindowState state)
    {
        if (ViewModel is { } vm)
            vm.IsFullscreen = state == WindowState.FullScreen;

        ApplyLayout();
    }

    // Fullscreen: the video fills the window above the control bar, on black. Otherwise a 16:9 player left-aligned
    // like YouTube: as wide as fits (up to 1280) but at most 70% of the height; see PlayerLayout.
    private void ApplyLayout()
    {
        var fullscreen = ViewModel?.IsFullscreen == true;
        Info.IsVisible = !fullscreen;
        Status.IsVisible = !fullscreen;
        Scroller.VerticalScrollBarVisibility = fullscreen ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Scroller.Background = fullscreen ? Brushes.Black : null;
        Scope.RequestedThemeVariant = fullscreen ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Default;
        Column.RowDefinitions[0].Height = fullscreen ? GridLength.Star : GridLength.Auto;
        Column.Margin = fullscreen ? default : PageMargin;
        Column.HorizontalAlignment = fullscreen ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Left;

        if (fullscreen)
        {
            Column.Width = double.NaN;
            Column.Height = Bounds.Height;
            VideoHost.Height = double.NaN;
            return;
        }

        Column.Height = double.NaN;
        var (width, height) = PlayerLayout.Size(Bounds.Width - PageMargin.Left - PageMargin.Right, Bounds.Height);
        Column.Width = width;
        VideoHost.Height = height;
    }

    // A click toggles play/pause once the double-click interval passes without a second click; a double click
    // toggles fullscreen only, so the first click of the pair never pauses.
    private void OnVideoTapped(object? sender, TappedEventArgs e)
    {
        _clicks.Click();
        Scroller.Focus();
    }

    private void OnVideoDoubleTapped(object? sender, TappedEventArgs e) => _clicks.DoubleClick();

    private void PlayPause_Click(object? sender, RoutedEventArgs e) => ViewModel?.TogglePlayPause();

    private void Fullscreen_Click(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (_window is not null)
            _window.WindowState = _window.WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    }

    private void OpenInBrowser_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            App.Services.FileSystemLauncher.TryOpenUrl(vm.WatchUrl);
    }

    private async void RetryDetails_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            await vm.LoadAsync();
    }

    private async void RetryComments_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            await vm.RetryCommentsAsync();
    }

    private void ToggleDescription_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        vm.ToggleDescription();
        DescriptionText.MaxLines = vm.IsDescriptionExpanded ? 0 : 3;
    }

    private void ViewDownloads_Click(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as MainWindow)?.OpenDownloadsPage();

    // Rebuilt whenever the profile list changes; the active profile is first and marked.
    private void RebuildDownloadMenu()
    {
        if (ViewModel is not { } vm || DownloadButton.Flyout is not MenuFlyout menu)
            return;

        var items = new List<MenuItem>();
        if (vm.Profiles.Count == 0)
            items.Add(new MenuItem { Header = "No profiles available", IsEnabled = false });

        for (var i = 0; i < vm.Profiles.Count; i++)
        {
            var profile = vm.Profiles[i];
            var item = new MenuItem { Header = i == 0 ? $"{profile.Name} (active)" : profile.Name };
            item.Click += async (_, _) => await vm.DownloadAsync(profile);
            items.Add(item);
        }

        menu.ItemsSource = items;
    }

    private sealed class ActionObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => onNext(value);
    }
}
