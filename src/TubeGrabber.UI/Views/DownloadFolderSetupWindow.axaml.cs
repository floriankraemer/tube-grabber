// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Interactivity;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Views;

public sealed partial class DownloadFolderSetupWindow : Window
{
    public DownloadFolderSetupWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
    }

    public DownloadFolderSetupWindow(string suggestedPath, string? errorMessage = null) : this()
    {
        FolderPathBox.Text = suggestedPath;
        if (!string.IsNullOrWhiteSpace(errorMessage))
            ErrorText.Text = errorMessage;
    }

    public string? SelectedPath { get; private set; }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var path = await App.Services.StoragePicker.PickFolderAsync(this);
        if (!string.IsNullOrWhiteSpace(path))
            FolderPathBox.Text = path;
    }

    private void Continue_Click(object? sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        var path = FolderPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ErrorText.Text = "Please enter a download folder.";
            return;
        }

        SelectedPath = path;
        Close(true);
    }
}
