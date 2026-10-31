// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class SearchView : UserControl
{
    private const double ScrollLoadThreshold = 200;

    private readonly SearchViewModel _viewModel;
    private bool _initialized;

    public SearchView()
    {
        InitializeComponent();
        _viewModel = App.Services.CreateSearchViewModel();
        DataContext = _viewModel;
    }

    /// <summary>Loads the page data the first time the page is shown; the view stays alive while hidden.</summary>
    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    /// <summary>Reloads the profile pickers once the page has been shown; before that, the first load will see the current profiles.</summary>
    public async Task RefreshProfilesAsync()
    {
        if (_initialized)
            await _viewModel.RefreshProfilesAsync();
    }

    private async void Search_Click(object? sender, RoutedEventArgs e) =>
        await _viewModel.SearchAsync();

    private async void ResultsScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        if (!_viewModel.CanLoadMore || _viewModel.IsSearching || _viewModel.IsLoadingMore)
            return;

        var extent = scrollViewer.Extent.Height;
        if (extent <= 0)
            return;

        var nearBottom = scrollViewer.Offset.Y + scrollViewer.Viewport.Height
            >= extent - ScrollLoadThreshold;

        if (nearBottom)
            await _viewModel.LoadMoreAsync();
    }

    private async void SearchQuery_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await _viewModel.SearchAsync();
    }

    private async void AddToQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SearchResultViewModel row })
            return;

        await row.AddToQueueAsync();
    }

    private async void Subscribe_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ChannelResultViewModel row })
            return;

        await row.SubscribeAsync();
    }

    private async void LoadMore_Click(object? sender, RoutedEventArgs e) =>
        await _viewModel.LoadMoreAsync();
}
