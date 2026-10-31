// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Interactivity;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

/// <summary>Asks for a profile name; used to create a profile (with a starting point) and to rename one.</summary>
public sealed partial class ProfileNameWindow : Window
{
    private const string DefaultsLabel = "yt-dlp defaults";

    private readonly ProfilesViewModel? _profiles;
    private readonly string? _exceptId;

    public ProfileNameWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    /// <summary>Create mode: name plus a "Start from" choice.</summary>
    public ProfileNameWindow(ProfilesViewModel profiles) : this()
    {
        _profiles = profiles;
        Title = "New profile";
        HeadingText.Text = "New profile";
        ConfirmButton.Content = "Create";
        NameBox.Text = profiles.SuggestNewName();

        var choices = new List<SourceChoice> { new(null, DefaultsLabel) };
        choices.AddRange(profiles.Cards.Select(card => new SourceChoice(card.Id, card.Name)));
        SourceBox.ItemsSource = choices;
        SourceBox.SelectedItem = choices.FirstOrDefault(c => c.Id == profiles.DefaultSourceId) ?? choices[0];
        NameBox.TextChanged += (_, _) => Validate();
        Validate();
    }

    /// <summary>Rename mode: name only; the profile's own current name is allowed.</summary>
    public ProfileNameWindow(ProfilesViewModel profiles, string profileId, string currentName) : this()
    {
        _profiles = profiles;
        _exceptId = profileId;
        Title = "Rename profile";
        HeadingText.Text = "Rename profile";
        ConfirmButton.Content = "Rename";
        SourcePanel.IsVisible = false;
        NameBox.Text = currentName;
        NameBox.TextChanged += (_, _) => Validate();
        Validate();
    }

    public string ProfileName => NameBox.Text?.Trim() ?? string.Empty;

    /// <summary>Id of the profile to copy, or null to start from the yt-dlp defaults.</summary>
    public string? SourceId => (SourceBox.SelectedItem as SourceChoice)?.Id;

    private void Validate()
    {
        var error = _profiles?.NameError(NameBox.Text ?? string.Empty, _exceptId);
        ErrorText.Text = error;
        ErrorRow.IsVisible = error is not null;
        ConfirmButton.IsEnabled = error is null;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        if (ConfirmButton.IsEnabled)
            Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private sealed record SourceChoice(string? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
