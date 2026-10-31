// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;
    private Task? _initialization;

    public SettingsView()
    {
        InitializeComponent();
        _viewModel = App.Services.CreateSettingsViewModel();
        DataContext = _viewModel;
        TabList.SelectionChanged += OnTabChanged;
        _viewModel.Saved += (_, _) => Saved?.Invoke(this, EventArgs.Empty);
        _viewModel.Profiles.ProfilesChanged += (_, _) => ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    // A 1000 px window leaves this much for the page once the sidebar has taken its 220.
    private const double NarrowWidth = 780;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
            Classes.Set("narrow", Bounds.Width < NarrowWidth);
    }

    /// <summary>Raised after a successful save, once the settings are on disk.</summary>
    public event EventHandler? Saved;

    /// <summary>Raised after a profile was created, duplicated or deleted, so the profile pickers of other pages can reload.</summary>
    public event EventHandler? ProfilesChanged;

    private SettingsViewModel ViewModel => _viewModel;

    /// <summary>Loads the page data once; concurrent callers (startup preload, first navigation) share the same load.</summary>
    public Task EnsureInitializedAsync() => _initialization ??= ViewModel.LoadAsync();

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        Control[] panels = [ProfilesPanel, AppearancePanel, BackgroundPanel, BinariesPanel, QueuePanel];
        for (var i = 0; i < panels.Length; i++)
            panels[i].IsVisible = i == TabList.SelectedIndex;
    }

    private async void Save_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.SaveAsync();

    private async void Discard_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        ViewModel.StatusMessage = "Changes discarded.";
    }

    /// <summary>
    /// Asks what to do with unsaved changes before the page is left. Returns false when the user wants to stay
    /// (Cancel, or Save failed).
    /// </summary>
    public Task<bool> ConfirmLeaveAsync() => ViewModel.ConfirmLeaveAsync(AskUnsavedChangesAsync);

    private async Task<UnsavedChangesChoice> AskUnsavedChangesAsync()
    {
        var dirty = ViewModel.Profiles.DirtyProfileCount;
        var detail = dirty > 0 ? ViewModel.UnsavedSummary + "." : "You changed settings that are not saved yet.";
        var dialog = new ConfirmWindow("Save your changes?", detail, [], "Save", secondaryText: "Discard");
        return await ShowDialogAsync(dialog) switch
        {
            ConfirmChoice.Primary => UnsavedChangesChoice.Save,
            ConfirmChoice.Secondary => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    private async Task<ConfirmChoice> ShowDialogAsync(ConfirmWindow dialog) =>
        TopLevel.GetTopLevel(this) is Window owner ? await dialog.ShowDialog<ConfirmChoice>(owner) : ConfirmChoice.Cancel;

    private async void InstallYtDlp_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.InstallYtDlpAsync();

    private async void InstallFfmpeg_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.InstallFfmpegAsync();

    private async void InstallDeno_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.InstallDenoAsync();

    private void OpenYtDlpReleases_Click(object? sender, RoutedEventArgs e) =>
        ViewModel.OpenYtDlpReleasesPage();

    private void OpenFfmpegReleases_Click(object? sender, RoutedEventArgs e) =>
        ViewModel.OpenFfmpegReleasesPage();

    private void OpenDenoReleases_Click(object? sender, RoutedEventArgs e) =>
        ViewModel.OpenDenoReleasesPage();

    private async void TestYtDlp_Click(object? sender, RoutedEventArgs e)
    {
        var result = await ViewModel.TestYtDlpAsync();
        ViewModel.ApplyTestResult("yt-dlp", result);
    }

    private async void TestFfmpeg_Click(object? sender, RoutedEventArgs e)
    {
        var result = await ViewModel.TestFfmpegAsync();
        ViewModel.ApplyTestResult("ffmpeg", result);
    }

    private async void TestJsRuntime_Click(object? sender, RoutedEventArgs e)
    {
        var result = await ViewModel.TestJsRuntimeAsync();
        ViewModel.ApplyTestResult("JavaScript runtime", result);
    }

    private async void BrowseYtDlp_Click(object? sender, RoutedEventArgs e) =>
        await BrowseExecutableAsync(path => ViewModel.YtDlpPath = path);

    private async void BrowseFfmpeg_Click(object? sender, RoutedEventArgs e) =>
        await BrowseExecutableAsync(path => ViewModel.FfmpegPath = path);

    private async void BrowseJsRuntime_Click(object? sender, RoutedEventArgs e) =>
        await BrowseExecutableAsync(path => ViewModel.JsRuntimePath = path);

    private async void BrowseDownloadFolder_Click(object? sender, RoutedEventArgs e)
    {
        var path = await App.Services.StoragePicker.PickFolderAsync(TopLevel.GetTopLevel(this)!);
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel.DownloadFolder = path;
    }

    private async Task BrowseExecutableAsync(Action<string> assignPath)
    {
        var path = await App.Services.StoragePicker.PickExecutableAsync(TopLevel.GetTopLevel(this)!);
        if (!string.IsNullOrWhiteSpace(path))
            assignPath(path);
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => ViewModel.Profiles.CloseProfile();

    private void OpenProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is { } card)
            ViewModel.Profiles.OpenProfile(card.Id);
    }

    private void OpenFailingProfile_Click(object? sender, RoutedEventArgs e) => ViewModel.OpenFailingProfile();

    private async void NewProfile_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new ProfileNameWindow(ViewModel.Profiles);
        if (TopLevel.GetTopLevel(this) is Window owner && await dialog.ShowDialog<bool>(owner))
            await ViewModel.Profiles.CreateProfileAsync(dialog.ProfileName, dialog.SourceId);
    }

    /// <summary>Menu key or Shift+F10 on a card opens the same menu as its more-actions button.</summary>
    private static void Card_KeyDown(object? sender, KeyEventArgs e)
    {
        var isMenuKey = e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift);
        if (!isMenuKey || sender is not Button { Parent: Panel panel } card)
            return;

        if (panel.Children.OfType<Button>().FirstOrDefault(b => !ReferenceEquals(b, card)) is { Flyout: { } flyout } more)
        {
            flyout.ShowAt(more);
            e.Handled = true;
        }
    }

    private void EditProfileMenu_Click(object? sender, RoutedEventArgs e) => OpenProfile_Click(sender, e);

    private async void DuplicateProfileMenu_Click(object? sender, RoutedEventArgs e) =>
        await DuplicateAsync(CardOf(sender));

    private async void DuplicateFromDetail_Click(object? sender, RoutedEventArgs e) =>
        await DuplicateAsync(ViewModel.Profiles.EditingProfile);

    private void ResetProfileMenu_Click(object? sender, RoutedEventArgs e) => ResetToBuiltIn(CardOf(sender));

    private void ResetFromDetail_Click(object? sender, RoutedEventArgs e) => ResetToBuiltIn(ViewModel.Profiles.EditingProfile);

    private async void DeleteProfileMenu_Click(object? sender, RoutedEventArgs e) => await DeleteAsync(CardOf(sender));

    private async void DeleteFromDetail_Click(object? sender, RoutedEventArgs e) => await DeleteAsync(ViewModel.Profiles.EditingProfile);

    private async void RenameProfile_Click(object? sender, RoutedEventArgs e) => await RenameAsync();

    private void ResetOption_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is OptionItemViewModel option)
            option.Reset();
    }

    private void EmptyAction_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Profiles.ShowChangedOnly)
            ViewModel.Profiles.ShowChangedOnly = false;
        else
            ViewModel.Profiles.SearchText = string.Empty;
    }

    private async void CopyPreview_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        await clipboard.SetTextAsync(ViewModel.Profiles.CliPreview);
        CopyPreviewText.Text = "Copied";
        await Task.Delay(1500);
        CopyPreviewText.Text = "Copy";
    }

    // The menu items live in a flyout; their DataContext is the card of the button that opened it.
    private static ProfileCardViewModel? CardOf(object? sender) => (sender as Control)?.DataContext as ProfileCardViewModel;

    private async Task DuplicateAsync(ProfileCardViewModel? card)
    {
        if (card is not null)
            await ViewModel.Profiles.DuplicateProfileAsync(card.Id);
    }

    private void ResetToBuiltIn(ProfileCardViewModel? card)
    {
        if (card is not null)
            ViewModel.Profiles.ResetToBuiltIn(card.Id);
    }

    private async Task RenameAsync()
    {
        if (ViewModel.Profiles is not { CanRename: true, EditingProfile: { } card } profiles)
            return;

        var dialog = new ProfileNameWindow(profiles, card.Id, card.Name);
        if (TopLevel.GetTopLevel(this) is Window owner && await dialog.ShowDialog<bool>(owner))
            profiles.Rename(card.Id, dialog.ProfileName);
    }

    private async Task DeleteAsync(ProfileCardViewModel? card)
    {
        if (card is null || !ViewModel.Profiles.CanDeleteProfile(card.Id))
            return;

        var warnings = new List<string>();
        if (card.SubscriptionCount > 0)
        {
            var noun = ProfileCardViewModel.SubscriptionNoun(card.SubscriptionCount);
            var verb = card.SubscriptionCount == 1 ? "uses" : "use";
            var pronoun = card.SubscriptionCount == 1 ? "It" : "They";
            warnings.Add($"{card.SubscriptionCount} {noun} {verb} this profile. {pronoun} will switch to Default.");
        }

        if (card.IsDirty)
            warnings.Add("Its unsaved changes will be lost.");

        var dialog = new ConfirmWindow(
            $"Delete \u201c{card.Name}\u201d?",
            "This removes the profile from this computer. You can\u2019t undo this.",
            warnings,
            "Delete",
            primaryIsDanger: true,
            focusCancel: true);
        if (await ShowDialogAsync(dialog) == ConfirmChoice.Primary)
            await ViewModel.Profiles.DeleteProfileAsync(card.Id);
    }

    /// <summary>Keyboard shortcuts of the profile detail page: Ctrl+F finds, F2 renames, Esc or Alt+Left goes back.</summary>
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !ViewModel.Profiles.IsEditing || !ProfilesPanel.IsVisible)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            OptionSearchBox.Focus();
            OptionSearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            await RenameAsync();
        }
        else if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt)
        {
            ViewModel.Profiles.CloseProfile();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (ReferenceEquals(focused, OptionSearchBox) && ViewModel.Profiles.HasQuery)
                ViewModel.Profiles.SearchText = string.Empty;
            else if (focused is not TextBox)
                ViewModel.Profiles.CloseProfile();

            e.Handled = true;
        }
    }
}
