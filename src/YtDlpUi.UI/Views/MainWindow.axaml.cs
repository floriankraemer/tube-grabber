// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YtDlpUi.UI.Services;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Views;

public sealed partial class MainWindow : Window
{
    private const int ColumnSaveDebounceMs = 400;

    private CancellationTokenSource? _columnSaveDebounce;
    private DownloadJobViewModel? _contextMenuJob;
    private SearchWindow? _searchWindow;
    private SubscriptionsWindow? _subscriptionsWindow;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpenedAsync;
        Closing += OnClosingAsync;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnOpenedAsync(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.InitializeAsync();

        if (await vm.RequiresDownloadFolderSetupAsync())
            await PromptForDownloadFolderAsync(vm);

        var widths = await vm.LoadQueueColumnWidthsAsync();
        QueueColumnLayout.Apply(JobsDataGrid, widths);

        foreach (var column in JobsDataGrid.Columns)
            column.PropertyChanged += OnColumnPropertyChanged;
    }

    private async void OnClosingAsync(object? sender, WindowClosingEventArgs e)
    {
        await SaveColumnWidthsAsync();
    }

    private void OnColumnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name != nameof(DataGridColumn.Width))
            return;

        _columnSaveDebounce?.Cancel();
        _columnSaveDebounce = new CancellationTokenSource();
        var token = _columnSaveDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ColumnSaveDebounceMs, token);
                await Dispatcher.UIThread.InvokeAsync(SaveColumnWidthsAsync);
            }
            catch (OperationCanceledException)
            {
                // Debounce restarted.
            }
        }, token);
    }

    private async Task PromptForDownloadFolderAsync(MainWindowViewModel vm)
    {
        var suggested = vm.GetSuggestedDownloadFolder();
        string? errorMessage = null;

        while (await vm.RequiresDownloadFolderSetupAsync())
        {
            var dialog = new DownloadFolderSetupWindow(suggested, errorMessage);
            var accepted = await dialog.ShowDialog<bool>(this);
            if (!accepted || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                vm.SetErrorMessage("A download folder is required. Restart the app to choose one.");
                return;
            }

            var (saved, error) = await vm.SaveDownloadFolderAsync(dialog.SelectedPath);
            if (saved)
                return;

            suggested = dialog.SelectedPath;
            errorMessage = error;
        }
    }

    private async Task SaveColumnWidthsAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var widths = QueueColumnLayout.Capture(JobsDataGrid);
        if (widths.Count == 0)
            return;

        await vm.SaveQueueColumnWidthsAsync(widths);
    }

    private async void Add_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.NormalizeUrlInput();
        await ViewModel.AddUrlAsync();
    }

    private async void UrlInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        ViewModel.NormalizeUrlInput();
        await ViewModel.AddUrlAsync();
    }

    private async void StartJob_Click(object? sender, RoutedEventArgs e)
    {
        if (ResolveJob(sender) is not { } job)
            return;

        await ViewModel.StartJobAsync(job);
    }

    private async void ViewLog_Click(object? sender, RoutedEventArgs e)
    {
        if (ResolveJob(sender) is not DownloadJobViewModel job)
            return;

        var logText = string.IsNullOrWhiteSpace(job.LogOutput)
            ? job.Error ?? "No log output was captured for this download."
            : job.LogOutput;

        var window = new JobLogWindow($"yt-dlp log — {job.Title}", logText);
        await window.ShowDialog(this);
    }

    private async void CancelJob_Click(object? sender, RoutedEventArgs e)
    {
        if (ResolveJob(sender) is not { } job)
            return;

        await ViewModel.CancelJobAsync(job);
    }

    private async void RemoveJob_Click(object? sender, RoutedEventArgs e)
    {
        if (ResolveJob(sender) is not { } job)
            return;

        await ViewModel.RemoveJobAsync(job);
    }

    private void JobsDataGrid_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right || sender is not DataGrid dataGrid)
            return;

        if (TryGetJobFromPointer(dataGrid, e) is { } job)
            dataGrid.SelectedItem = job;
    }

    private void JobContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        if (JobsDataGrid.SelectedItem is not DownloadJobViewModel job)
        {
            e.Cancel = true;
            return;
        }

        _contextMenuJob = job;

        var hasOpenOutput = job.CanOpenOutput;
        OpenOutputMenuItem.Header = job.OpenOutputMenuLabel;
        OpenOutputMenuItem.IsVisible = hasOpenOutput;
        OpenOutputSeparator.IsVisible = hasOpenOutput;

        StartJobMenuItem.IsVisible = job.CanStart;
        ViewLogMenuItem.IsVisible = job.CanViewLog;
        CancelJobMenuItem.IsVisible = job.CanCancel;
        RemoveJobMenuItem.IsVisible = job.CanRemove;

        if (!hasOpenOutput && !job.CanStart && !job.CanViewLog && !job.CanCancel && !job.CanRemove)
            e.Cancel = true;
    }

    private void OpenOutput_Click(object? sender, RoutedEventArgs e)
    {
        if (ResolveJob(sender) is not DownloadJobViewModel jobViewModel)
            return;

        ViewModel.TryOpenOutput(jobViewModel);
    }

    private DownloadJobViewModel? ResolveJob(object? sender) =>
        GetJobFromSender(sender) ?? _contextMenuJob;

    private static DownloadJobViewModel? GetJobFromSender(object? sender) =>
        sender switch
        {
            MenuItem { DataContext: DownloadJobViewModel menuJob } => menuJob,
            Control { DataContext: DownloadJobViewModel controlJob } => controlJob,
            _ => null,
        };

    private static DownloadJobViewModel? TryGetJobFromPointer(DataGrid dataGrid, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(dataGrid).Position;
        var visual = dataGrid.GetVisualAt(point);
        while (visual is not null)
        {
            if (visual.DataContext is DownloadJobViewModel job)
                return job;

            visual = visual.GetVisualParent();
        }

        return dataGrid.SelectedItem as DownloadJobViewModel;
    }

    private void Search_Click(object? sender, RoutedEventArgs e)
    {
        if (_searchWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        _searchWindow = new SearchWindow();
        _searchWindow.Closed += OnSearchWindowClosed;
        _searchWindow.Show(this);
    }

    private void OnSearchWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
            window.Closed -= OnSearchWindowClosed;

        _searchWindow = null;
    }

    private void Subscriptions_Click(object? sender, RoutedEventArgs e) => OpenSubscriptionsWindow();

    public void OpenSubscriptionsWindow()
    {
        if (_subscriptionsWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        _subscriptionsWindow = new SubscriptionsWindow();
        _subscriptionsWindow.Closed += OnSubscriptionsWindowClosed;
        _subscriptionsWindow.Show(this);
    }

    private void OnSubscriptionsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
            window.Closed -= OnSubscriptionsWindowClosed;

        _subscriptionsWindow = null;
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow();
        await window.ShowDialog(this);
        await ViewModel.RefreshProfilesAsync();
    }
}
