// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class SubscriptionsView : UserControl
{
    private readonly SubscriptionsViewModel _viewModel;
    private bool _initialized;

    public SubscriptionsView()
    {
        InitializeComponent();
        _viewModel = App.Services.CreateSubscriptionsViewModel();
        DataContext = _viewModel;
    }

    /// <summary>Detaches the view model from the subscription monitor; called once at app shutdown.</summary>
    public void DisposeViewModel() => _viewModel.Dispose();

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
}
