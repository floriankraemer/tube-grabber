// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Views;

public sealed partial class SubscriptionsWindow : Window
{
    private readonly SubscriptionsViewModel _viewModel;

    public SubscriptionsWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
        _viewModel = App.Services.CreateSubscriptionsViewModel();
        DataContext = _viewModel;
        Opened += async (_, _) => await _viewModel.InitializeAsync();
        Closed += (_, _) => _viewModel.Dispose();
    }

    private async void Subscribe_Click(object? sender, RoutedEventArgs e) =>
        await _viewModel.SubscribeAsync();

    private async void CheckNow_Click(object? sender, RoutedEventArgs e) =>
        await _viewModel.CheckNowAsync();

    private async void ChannelUrl_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await _viewModel.SubscribeAsync();
    }

    private async void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SubscriptionItemViewModel row })
            await row.RemoveAsync();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
