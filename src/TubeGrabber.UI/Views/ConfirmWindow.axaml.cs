// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Interactivity;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Views;

/// <summary>The button a <see cref="ConfirmWindow"/> was closed with. Closing the window any other way counts as Cancel.</summary>
public enum ConfirmChoice
{
    Cancel,
    Primary,
    Secondary,
}

/// <summary>A modal question with up to three answers: a primary action (optionally destructive), an optional second action and Cancel.</summary>
public sealed partial class ConfirmWindow : Window
{
    private readonly Button _cancelButton = new() { Content = "Cancel", IsCancel = true };

    public ConfirmWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
    }

    public ConfirmWindow(
        string heading,
        string? body,
        IReadOnlyList<string> warnings,
        string primaryText,
        bool primaryIsDanger = false,
        string? secondaryText = null,
        bool focusCancel = false) : this()
    {
        Title = heading;
        HeadingText.Text = heading;
        BodyText.Text = body;
        BodyText.IsVisible = !string.IsNullOrEmpty(body);
        WarningList.ItemsSource = warnings;
        WarningList.IsVisible = warnings.Count > 0;

        var primary = new Button { Content = primaryText, FontWeight = Avalonia.Media.FontWeight.SemiBold };
        primary.Classes.Add(primaryIsDanger ? "danger" : "accent");
        primary.IsDefault = !focusCancel;
        primary.Click += (_, _) => Close(ConfirmChoice.Primary);

        var buttons = new List<Button> { primary };
        if (secondaryText is not null)
        {
            var secondary = new Button { Content = secondaryText };
            secondary.Click += (_, _) => Close(ConfirmChoice.Secondary);
            buttons.Add(secondary);
        }

        _cancelButton.Click += (_, _) => Close(ConfirmChoice.Cancel);
        buttons.Add(_cancelButton);

        for (var i = 0; i < buttons.Count; i++)
        {
            ActionsHost.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            buttons[i].HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            buttons[i].HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            Grid.SetColumn(buttons[i], i);
            ActionsHost.Children.Add(buttons[i]);
        }

        // Destructive questions open on Cancel, so a stray Enter never deletes anything.
        Opened += (_, _) => (focusCancel ? _cancelButton : primary).Focus();
    }
}
