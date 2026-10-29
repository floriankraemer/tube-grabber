// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YtDlpUi.UI.Views;

public sealed partial class JobLogWindow : Window
{
    public JobLogWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
    }

    public JobLogWindow(string title, string logText) : this()
    {
        Title = title;
        LogTextBox.Text = logText;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
