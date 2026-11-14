// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class WatchView : UserControl
{
    private const double MaxColumnWidth = 960;
    private const double MaxVideoHeightShare = 0.6;
    private const double VideoAspect = 9.0 / 16.0;

    private Window? _window;
    private IDisposable? _windowStateSubscription;

    public WatchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => ApplyLayout();
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
            Video.MediaPlayer = vlc.MediaPlayer;

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

    // 16:9 video up to 960 wide, left-aligned like YouTube; the whole window in fullscreen.
    private void ApplyLayout()
    {
        var fullscreen = ViewModel?.IsFullscreen == true;
        Info.IsVisible = !fullscreen;
        Status.IsVisible = !fullscreen;
        Column.RowDefinitions[0].Height = fullscreen ? GridLength.Star : GridLength.Auto;
        Column.RowDefinitions[3].Height = fullscreen ? GridLength.Auto : GridLength.Star;
        Column.HorizontalAlignment = fullscreen ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Left;
        // The video takes at most ~60% of the height so the title and Download stay visible without scrolling.
        var widthForHeight = Bounds.Height * MaxVideoHeightShare / VideoAspect;
        Column.Width = fullscreen ? double.NaN : Math.Max(0, Math.Min(Math.Min(MaxColumnWidth, Bounds.Width), widthForHeight));
        VideoHost.Height = fullscreen ? double.NaN : Column.Width * VideoAspect;
        VideoHost.CornerRadius = new CornerRadius(fullscreen ? 0 : 6);
    }

    private void PlayPause_Click(object? sender, RoutedEventArgs e) => ViewModel?.TogglePlayPause();

    private void Fullscreen_Click(object? sender, RoutedEventArgs e)
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
