// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Views;

public sealed partial class MainWindow : Window
{
    private bool _initialized;
    private bool _backgroundHintShown;
    private bool _syncingNav;
    private MainWindowViewModel? _observedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
        // The platform may already have answered the hints while InitializeComponent applied them.
        UpdateChrome();
        // macOS keeps its native traffic lights at the left end of the title row.
        if (OperatingSystem.IsMacOS())
            TitleContent.Margin = new Thickness(80, 0, 0, 0);
        Opened += OnOpenedAsync;
        Closing += OnClosingAsync;
        DataContextChanged += OnDataContextChanged;
        SettingsPage.Saved += OnSettingsSavedAsync;
        SettingsPage.ProfilesChanged += OnProfilesChangedAsync;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    /// <summary>Shows the Subscriptions page; used by the tray menu after the window has been brought to the front.</summary>
    public void OpenSubscriptionsPage() => _ = NavigateAsync(AppPage.Subscriptions);

    /// <summary>Releases view models that hold event subscriptions; called once when the app quits.</summary>
    public void DisposePages() => SubscriptionsPage.DisposeViewModel();

    public Task SaveStateAsync() => DownloadsPage.SaveStateAsync();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActualTransparencyLevelProperty
            || change.Property == WindowDecorationMarginProperty
            || change.Property == IsExtendedIntoWindowDecorationsProperty)
            UpdateChrome();
    }

    private void UpdateChrome()
    {
        // Raised while InitializeComponent is still applying the window properties.
        if (RootGrid is null)
            return;

        Classes.Set("solid", !HasBackdropMaterial(ActualTransparencyLevel));
        RootGrid.Margin = ContentMargin(WindowDecorationMargin, ExtendClientAreaTitleBarHeightHint);

        // Under OS-drawn decorations (e.g. Linux window managers) the title bar already shows icon and title.
        var extended = IsExtendedIntoWindowDecorations;
        TitleContent.IsVisible = extended;
        RootGrid.RowDefinitions[0].Height = new GridLength(extended ? ExtendClientAreaTitleBarHeightHint : 8);
    }

    /// <summary>
    /// Only a real backdrop material is painted behind the transparent window background. Plain transparency
    /// (an X11 compositor) would show the desktop unblurred, so it gets the solid window token like None does.
    /// </summary>
    internal static bool HasBackdropMaterial(WindowTransparencyLevel level) =>
        level == WindowTransparencyLevel.Mica || level == WindowTransparencyLevel.AcrylicBlur || level == WindowTransparencyLevel.Blur;

    /// <summary>
    /// The decoration margin covers the title bar plus any drawn frame and shadow. The title row is part of the
    /// layout, so only the frame and shadow insets are applied to the content.
    /// </summary>
    internal static Thickness ContentMargin(Thickness decorationMargin, double titleBarHeight) =>
        new(decorationMargin.Left, Math.Max(0, decorationMargin.Top - titleBarHeight), decorationMargin.Right, decorationMargin.Bottom);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _observedViewModel = DataContext as MainWindowViewModel;
        if (_observedViewModel is null)
            return;

        _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncNavSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentPage))
            return;

        SyncNavSelection();
        _ = EnsureCurrentPageInitializedAsync();
    }

    private async Task EnsureCurrentPageInitializedAsync()
    {
        switch (ViewModel.CurrentPage)
        {
            case AppPage.Search:
                await SearchPage.EnsureInitializedAsync();
                break;
            case AppPage.Subscriptions:
                await SubscriptionsPage.EnsureInitializedAsync();
                break;
            case AppPage.Settings:
                await SettingsPage.EnsureInitializedAsync();
                break;
        }
    }

    private void SyncNavSelection()
    {
        var page = ViewModel.CurrentPage;
        _syncingNav = true;
        try
        {
            foreach (var item in NavList.Items.OfType<ListBoxItem>())
            {
                if (item.Tag is AppPage itemPage && itemPage == page)
                {
                    NavList.SelectedItem = item;
                    return;
                }
            }
        }
        finally
        {
            _syncingNav = false;
        }
    }

    private async void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Also raised while the XAML is being loaded, before a view model or the list itself exist.
        if (_syncingNav || DataContext is not MainWindowViewModel || NavList?.SelectedItem is not ListBoxItem { Tag: AppPage page })
            return;

        await NavigateAsync(page);
    }

    /// <summary>Shows a page. Leaving Settings with unsaved changes asks first; Cancel keeps the user on Settings.</summary>
    private async Task NavigateAsync(AppPage page)
    {
        if (DataContext is not MainWindowViewModel vm || vm.CurrentPage == page)
            return;

        if (vm.CurrentPage == AppPage.Settings)
        {
            // Put the selection back while the question is open; it moves again once the user agrees to leave.
            SyncNavSelection();
            if (!await SettingsPage.ConfirmLeaveAsync())
                return;
        }

        vm.CurrentPage = page;
    }

    private async void OnSettingsSavedAsync(object? sender, EventArgs e)
    {
        await App.RefreshRunInBackgroundAsync();
        await RefreshProfilePickersAsync();
    }

    private async void OnProfilesChangedAsync(object? sender, EventArgs e) => await RefreshProfilePickersAsync();

    private async Task RefreshProfilePickersAsync()
    {
        await ViewModel.RefreshProfilesAsync();
        await SearchPage.RefreshProfilesAsync();
        await SubscriptionsPage.RefreshProfilesAsync();
    }

    private async void OnOpenedAsync(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || _initialized)
            return;

        _initialized = true;
        await vm.InitializeAsync();

        if (await vm.RequiresDownloadFolderSetupAsync())
            await PromptForDownloadFolderAsync(vm);

        await DownloadsPage.InitializeColumnsAsync();
        await EnsureCurrentPageInitializedAsync();

        // Preload Settings so the profile list is ready instead of blank for a moment on first visit.
        await SettingsPage.EnsureInitializedAsync();
    }

    private async void OnClosingAsync(object? sender, WindowClosingEventArgs e)
    {
        // The quit path has already saved state and is shutting down; let the window close.
        if (App.IsQuitting)
            return;

        // Cancel before the first await: the window stays open while the async work completes.
        e.Cancel = true;
        if (TrayHost.ShouldHideOnClose(App.RunInBackground, App.TrayAvailable))
        {
            Hide();
            ShowBackgroundHint();
            await SaveStateAsync();
        }
        else
        {
            await App.QuitAsync();
        }
    }

    private void ShowBackgroundHint()
    {
        if (_backgroundHintShown)
            return;

        _backgroundHintShown = true;
        _ = App.Services.DesktopNotifier.ShowAsync(
            "Tube Grabber is still running",
            "Channel subscriptions keep being checked in the background. Use the tray icon to quit.");
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
}
