// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Views;

public sealed partial class SearchWindow : Window
{
    private const double ScrollLoadThreshold = 200;

    private readonly SearchViewModel _viewModel;

    public SearchWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
        _viewModel = App.Services.CreateSearchViewModel();
        DataContext = _viewModel;
        Opened += async (_, _) => await _viewModel.InitializeAsync();
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

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
