// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IDownloadQueueService _queue;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly DownloadEnqueueCoordinator _enqueueCoordinator;
    private readonly YouTubeUrlNormalizer _urlNormalizer;
    private readonly DownloadFolderService _downloadFolderService;
    private readonly IFileSystemLauncher _fileSystemLauncher;

    private string _urlInput = string.Empty;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _loadingProfiles;
    private AppPage _currentPage = AppPage.Downloads;
    private int _activeJobCount;
    private string? _downloadFolder;
    private string _downloadsSubtitle = QueueStatusMessageBuilder.BuildSubtitle([], null);
    private DownloadProfile? _selectedProfile;

    public MainWindowViewModel(
        IDownloadQueueService queue,
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        DownloadEnqueueCoordinator enqueueCoordinator,
        DownloadFolderService downloadFolderService,
        YouTubeUrlNormalizer urlNormalizer,
        IFileSystemLauncher fileSystemLauncher)
    {
        _queue = queue;
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _enqueueCoordinator = enqueueCoordinator;
        _downloadFolderService = downloadFolderService;
        _urlNormalizer = urlNormalizer;
        _fileSystemLauncher = fileSystemLauncher;
        Jobs = new ObservableCollection<DownloadJobViewModel>();
        Profiles = new ObservableCollection<DownloadProfile>();
        _queue.JobsChanged += (_, _) => ScheduleRefreshJobs();
    }

    public ObservableCollection<DownloadJobViewModel> Jobs { get; }

    public ObservableCollection<DownloadProfile> Profiles { get; }

    public DownloadProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value) || _loadingProfiles || value is null)
                return;

            _ = PersistActiveProfileAsync(value.Id);
        }
    }

    public bool HasJobs => Jobs.Count > 0;

    public AppPage CurrentPage
    {
        get => _currentPage;
        set
        {
            if (!SetProperty(ref _currentPage, value))
                return;

            OnPropertyChanged(nameof(IsDownloadsPage));
            OnPropertyChanged(nameof(IsSearchPage));
            OnPropertyChanged(nameof(IsSubscriptionsPage));
            OnPropertyChanged(nameof(IsSettingsPage));
            OnPropertyChanged(nameof(ShowStatusBar));

            // Transient errors belong to the page that raised them; per-job errors live in the queue row.
            ErrorMessage = null;
        }
    }

    public bool IsDownloadsPage => CurrentPage == AppPage.Downloads;

    public bool IsSearchPage => CurrentPage == AppPage.Search;

    public bool IsSubscriptionsPage => CurrentPage == AppPage.Subscriptions;

    public bool IsSettingsPage => CurrentPage == AppPage.Settings;

    /// <summary>Settings has its own sticky footer for save feedback, so the shared status bar is hidden there.</summary>
    public bool ShowStatusBar => CurrentPage != AppPage.Settings;

    /// <summary>Queued or running downloads; shown as the sidebar badge.</summary>
    public int ActiveJobCount
    {
        get => _activeJobCount;
        private set
        {
            if (SetProperty(ref _activeJobCount, value))
                OnPropertyChanged(nameof(HasActiveJobs));
        }
    }

    public bool HasActiveJobs => ActiveJobCount > 0;

    /// <summary>Header line of the Downloads page: live queue counts plus the target folder.</summary>
    public string DownloadsSubtitle
    {
        get => _downloadsSubtitle;
        private set => SetProperty(ref _downloadsSubtitle, value);
    }

    public string UrlInput
    {
        get => _urlInput;
        set => SetProperty(ref _urlInput, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public async Task InitializeAsync()
    {
        await _appConfigStore.EnsureBootstrapAsync();
        await RefreshProfilesAsync();
        _downloadFolder = (await _appConfigStore.LoadAsync()).DownloadFolder;
        SyncJobs();
    }

    public async Task RefreshProfilesAsync()
    {
        _loadingProfiles = true;
        try
        {
            var (profiles, selected) = await ProfileListLoader.LoadWithSelectionAsync(_profileStore, _appConfigStore);
            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(profile);

            _selectedProfile = selected;
            OnPropertyChanged(nameof(SelectedProfile));
        }
        finally
        {
            _loadingProfiles = false;
        }
    }

    public async Task<bool> RequiresDownloadFolderSetupAsync()
    {
        var config = await _appConfigStore.LoadAsync();
        return !_downloadFolderService.IsConfigured(config);
    }

    public string GetSuggestedDownloadFolder() => DownloadFolderService.GetSuggestedDefaultPath();

    public void SetErrorMessage(string? message) => ErrorMessage = message;

    public async Task<(bool Saved, string? Error)> SaveDownloadFolderAsync(string path)
    {
        if (!_downloadFolderService.TryNormalize(path, out var normalized))
            return (false, "Enter a valid download folder path.");

        if (!_downloadFolderService.EnsureExists(normalized))
            return (false, "Could not create folder.");

        var config = await _appConfigStore.LoadAsync();
        config.DownloadFolder = normalized;
        await _appConfigStore.SaveAsync(config);
        _downloadFolder = normalized;
        DownloadsSubtitle = QueueStatusMessageBuilder.BuildSubtitle(_queue.Jobs, _downloadFolder);
        StatusMessage = $"Downloads will be saved to {normalized}";
        return (true, null);
    }

    public async Task<IReadOnlyDictionary<string, double>> LoadQueueColumnWidthsAsync()
    {
        var config = await _appConfigStore.LoadAsync();
        return config.QueueColumnWidths;
    }

    public async Task SaveQueueColumnWidthsAsync(IReadOnlyDictionary<string, double> widths)
    {
        var config = await _appConfigStore.LoadAsync();
        config.QueueColumnWidths = widths.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);
        await _appConfigStore.SaveAsync(config);
    }

    public async Task AddUrlAsync()
    {
        ErrorMessage = null;
        var config = await _appConfigStore.LoadAsync();
        var profileId = SelectedProfile?.Id ?? config.ActiveProfileId;
        var result = await _enqueueCoordinator.TryEnqueueAsync(UrlInput, profileId ?? string.Empty);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
            return;
        }

        UrlInput = string.Empty;
        SyncJobs();
        StatusMessage = Jobs.Count == 1
            ? "1 download in queue."
            : $"{Jobs.Count} downloads in queue.";
    }

    public void NormalizeUrlInput()
    {
        var result = _urlNormalizer.Normalize(UrlInput);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.NormalizedUrl))
            UrlInput = result.NormalizedUrl;
    }

    public async Task StartJobAsync(DownloadJobViewModel job)
    {
        ErrorMessage = null;
        await _queue.StartJobAsync(job.Id);
        SyncJobs();
        StatusMessage = "Download started.";
    }

    public async Task CancelJobAsync(DownloadJobViewModel job)
    {
        await _queue.CancelAsync(job.Id);
        SyncJobs();
    }

    public async Task RemoveJobAsync(DownloadJobViewModel job)
    {
        await _queue.RemoveAsync(job.Id);
        SyncJobs();
    }

    public bool TryOpenOutput(DownloadJobViewModel jobViewModel)
    {
        var job = _queue.Jobs.FirstOrDefault(item => item.Id == jobViewModel.Id);
        if (job is null)
        {
            ErrorMessage = "Download is no longer in the queue.";
            return false;
        }

        var target = DownloadOutputResolver.Resolve(job);
        if (!target.CanOpen)
        {
            ErrorMessage = "Could not find the downloaded file.";
            return false;
        }

        var opened = target.IsSingleFile
            ? _fileSystemLauncher.TryOpenFile(target.Path)
            : _fileSystemLauncher.TryOpenLocation(target.Path);

        if (!opened)
            ErrorMessage = "Could not open the download location.";

        return opened;
    }

    private async Task PersistActiveProfileAsync(string profileId)
    {
        var config = await _appConfigStore.LoadAsync();
        if (string.Equals(config.ActiveProfileId, profileId, StringComparison.Ordinal))
            return;

        config.ActiveProfileId = profileId;
        await _appConfigStore.SaveAsync(config);
    }

    private void ScheduleRefreshJobs()
    {
        // No Application (e.g. unit tests) means no UI thread to marshal to.
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            SyncJobs();
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(SyncJobs);
    }

    private void SyncJobs()
    {
        var queueJobs = _queue.Jobs;
        var queueIds = queueJobs.Select(j => j.Id).ToHashSet();

        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            if (!queueIds.Contains(Jobs[i].Id))
                Jobs.RemoveAt(i);
        }

        foreach (var job in queueJobs)
        {
            var existing = Jobs.FirstOrDefault(j => j.Id == job.Id);
            if (existing is null)
                Jobs.Add(new DownloadJobViewModel(job));
            else
                existing.Refresh();
        }

        OnPropertyChanged(nameof(HasJobs));
        ActiveJobCount = queueJobs.Count(j => j.Status is DownloadStatus.Queued or DownloadStatus.Running);

        StatusMessage = QueueStatusMessageBuilder.Build(queueJobs);
        DownloadsSubtitle = QueueStatusMessageBuilder.BuildSubtitle(queueJobs, _downloadFolder);
    }
}
