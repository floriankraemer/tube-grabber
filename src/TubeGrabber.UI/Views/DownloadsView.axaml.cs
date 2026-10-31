// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

/// <summary>The download queue page. Its DataContext is the <see cref="MainWindowViewModel"/> inherited from the window.</summary>
public sealed partial class DownloadsView : UserControl
{
    private const int ColumnSaveDebounceMs = 400;

    private readonly SemaphoreSlim _stateSaveLock = new(1, 1);
    private CancellationTokenSource? _columnSaveDebounce;
    private DownloadJobViewModel? _contextMenuJob;

    public DownloadsView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    /// <summary>Applies the persisted column widths and starts persisting later resizes.</summary>
    public async Task InitializeColumnsAsync()
    {
        var widths = await ViewModel.LoadQueueColumnWidthsAsync();
        QueueColumnLayout.Apply(JobsDataGrid, widths);

        foreach (var column in JobsDataGrid.Columns)
            column.PropertyChanged += OnColumnPropertyChanged;
    }

    public async Task SaveStateAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        _columnSaveDebounce?.Cancel();

        // Serialized because each save is a read-modify-write of the shared config file.
        await _stateSaveLock.WaitAsync();
        try
        {
            var widths = QueueColumnLayout.Capture(JobsDataGrid);
            if (widths.Count > 0)
                await vm.SaveQueueColumnWidthsAsync(widths);
        }
        finally
        {
            _stateSaveLock.Release();
        }
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
                await Dispatcher.UIThread.InvokeAsync(SaveStateAsync);
            }
            catch (OperationCanceledException)
            {
                // Debounce restarted.
            }
        }, token);
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
        if (ResolveJob(sender) is not DownloadJobViewModel job || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var logText = string.IsNullOrWhiteSpace(job.LogOutput)
            ? job.Error ?? "No log output was captured for this download."
            : job.LogOutput;

        var window = new JobLogWindow($"yt-dlp log — {job.Title}", logText);
        await window.ShowDialog(owner);
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
}
