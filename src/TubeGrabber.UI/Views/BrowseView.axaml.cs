// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class BrowseView : UserControl
{
    private const double ScrollLoadThreshold = 300;

    private readonly BrowseViewModel _viewModel;
    private bool _initialized;

    public BrowseView()
    {
        InitializeComponent();
        _viewModel = App.Services.CreateBrowseViewModel();
        _viewModel.SubscriptionsRequested += (_, _) => SubscriptionsRequested?.Invoke(this, EventArgs.Empty);
        DataContext = _viewModel;
    }

    /// <summary>Raised when the empty-feed hint asks to open the Subscriptions page.</summary>
    public event EventHandler? SubscriptionsRequested;

    /// <summary>Loads the feed the first time the page is shown; the view stays alive while hidden.</summary>
    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    public void OpenWatch(YouTubeSearchResult video) => _viewModel.OpenWatch(video);

    private async void Search_Click(object? sender, RoutedEventArgs e) => await _viewModel.SearchAsync();

    private async void ClearSearch_Click(object? sender, RoutedEventArgs e) => await _viewModel.ClearSearchAsync();

    private async void Retry_Click(object? sender, RoutedEventArgs e) => await _viewModel.RetryAsync();

    private async void LoadMore_Click(object? sender, RoutedEventArgs e) => await _viewModel.LoadMoreAsync();

    private void Subscriptions_Click(object? sender, RoutedEventArgs e) => _viewModel.RequestSubscriptions();

    private void Back_Click(object? sender, RoutedEventArgs e) => _viewModel.Back();

    private void Tile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: VideoTileViewModel tile })
            _viewModel.OpenWatch(tile.Result);
    }

    private async void SearchQuery_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await _viewModel.SearchAsync();
    }

    private async void TilesScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || !_viewModel.CanLoadMore || _viewModel.IsLoading || _viewModel.IsLoadingMore)
            return;

        var extent = scrollViewer.Extent.Height;
        if (extent > 0 && scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= extent - ScrollLoadThreshold)
            await _viewModel.LoadMoreAsync();
    }
}
