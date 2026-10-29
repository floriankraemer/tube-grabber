// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using YtDlpUi.Core.Models;
using YtDlpUi.UI.Services;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly Dictionary<string, Control> _sectionPanels = new();
    private readonly List<string> _sectionOrder = [];

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
        _viewModel = App.Services.CreateSettingsViewModel();
        DataContext = _viewModel;
        Opened += async (_, _) =>
        {
            await _viewModel.LoadAsync();
            ProfilesList.ItemTemplate = new FuncDataTemplate<DownloadProfile>((profile, _) =>
                new TextBlock { Text = profile?.Name ?? string.Empty });
            InitializeSections();
            ProfilesList.SelectionChanged += async (_, _) =>
            {
                if (ProfilesList.SelectedItem is DownloadProfile profile)
                    await _viewModel.SelectProfileAsync(profile);
            };

            if (ProfilesList.SelectedItem is null && _viewModel.Profiles.Count > 0)
                ProfilesList.SelectedIndex = 0;
        };
    }

    private SettingsViewModel ViewModel => _viewModel;

    private void InitializeSections()
    {
        RegisterSection("Profiles", ProfilesPanel);
        RegisterSection("Appearance", AppearancePanel);
        RegisterSection("Background", BackgroundPanel);
        RegisterSection("Binaries", BinariesPanel);
        RegisterSection("Queue", QueuePanel);
        BuildOptionSections();
        RegisterSection("Advanced", AdvancedPanel);

        SectionList.ItemsSource = _sectionOrder;
        SectionList.SelectionChanged += (_, _) =>
        {
            if (SectionList.SelectedItem is not string name)
                return;

            foreach (var panel in _sectionPanels.Values)
                panel.IsVisible = false;

            if (_sectionPanels.TryGetValue(name, out var selected))
                selected.IsVisible = true;
        };

        SectionList.SelectedIndex = 0;
    }

    private void RegisterSection(string name, Control panel)
    {
        _sectionOrder.Add(name);
        _sectionPanels[name] = panel;
    }

    private void BuildOptionSections()
    {
        foreach (var section in ViewModel.OptionSections)
        {
            var scroll = new ScrollViewer();
            var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8) };

            foreach (var option in section.Options)
            {
                var border = new Border
                {
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 4),
                    BorderThickness = new Thickness(1),
                };

                var stack = new StackPanel { Spacing = 4 };
                ToolTip.SetTip(stack, option.Tooltip);
                stack.Children.Add(new TextBlock { Text = option.Flag, FontWeight = Avalonia.Media.FontWeight.SemiBold });

                if (option.IsBool)
                {
                    var checkBox = new CheckBox { Content = "Enabled", IsChecked = option.BoolValue };
                    checkBox.IsCheckedChanged += (_, _) => option.BoolValue = checkBox.IsChecked == true;
                    option.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(OptionItemViewModel.BoolValue))
                            checkBox.IsChecked = option.BoolValue;
                    };
                    stack.Children.Add(checkBox);
                }
                else if (option.IsChoice)
                {
                    var combo = new ComboBox { ItemsSource = option.Choices, SelectedItem = option.StringValue };
                    combo.SelectionChanged += (_, _) => option.StringValue = combo.SelectedItem?.ToString() ?? string.Empty;
                    stack.Children.Add(combo);
                }
                else
                {
                    var textBox = new TextBox { Text = option.StringValue };
                    textBox.TextChanged += (_, _) => option.StringValue = textBox.Text ?? string.Empty;
                    stack.Children.Add(textBox);
                }

                border.Child = stack;
                panel.Children.Add(border);
            }

            scroll.Content = panel;
            SectionPanelsHost.Children.Add(scroll);
            RegisterSection(section.Name, scroll);
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (await ViewModel.SaveAsync())
            Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

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
        var path = await App.Services.StoragePicker.PickFolderAsync(this);
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel.DownloadFolder = path;
    }

    private async Task BrowseExecutableAsync(Action<string> assignPath)
    {
        var path = await App.Services.StoragePicker.PickExecutableAsync(this);
        if (!string.IsNullOrWhiteSpace(path))
            assignPath(path);
    }

    private async void CreateProfile_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.CreateProfileAsync();

    private async void DuplicateProfile_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.DuplicateProfileAsync();

    private async void DeleteProfile_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.DeleteProfileAsync();
}
