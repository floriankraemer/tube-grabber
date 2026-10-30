// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;
using YtDlpUi.UI.Services;

namespace YtDlpUi.UI.ViewModels;

public sealed class SearchViewModel : ViewModelBase
{
    private readonly IYtDlpSearchService _searchService;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly DownloadEnqueueCoordinator _enqueueCoordinator;
    private readonly IThumbnailLoader _thumbnailLoader;
    private readonly ISubscriptionMonitor _subscriptionMonitor;

    // A page can come back empty once Shorts are filtered out; keep paging a few times before giving up.
    private const int MaxEmptyPagesInARow = 5;

    private string _searchQuery = string.Empty;
    private bool _isChannelMode;
    private bool _hideShorts = true;
    private FilterOption<UploadDateFilter> _selectedUploadDate;
    private FilterOption<DurationFilter> _selectedDuration;
    private bool _hasSearched;
    private string _activeQuery = string.Empty;
    private bool _activeChannelMode;
    private SearchFilters _activeFilters = SearchFilters.Default;
    private int _nextSkip;
    private CancellationTokenSource? _searchCts;
    private bool _isSearching;
    private bool _isLoadingMore;
    private bool _canLoadMore;
    private string? _statusMessage;
    private string? _errorMessage;
    private CancellationTokenSource? _thumbnailLoadCts;

    public SearchViewModel(
        IYtDlpSearchService searchService,
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        DownloadEnqueueCoordinator enqueueCoordinator,
        IThumbnailLoader thumbnailLoader,
        ISubscriptionMonitor subscriptionMonitor)
    {
        _searchService = searchService;
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _enqueueCoordinator = enqueueCoordinator;
        _thumbnailLoader = thumbnailLoader;
        _subscriptionMonitor = subscriptionMonitor;
        Results = new ObservableCollection<SearchResultViewModel>();
        Channels = new ObservableCollection<ChannelResultViewModel>();
        Profiles = new ObservableCollection<DownloadProfile>();
        _selectedUploadDate = UploadDateOptions[0];
        _selectedDuration = DurationOptions[0];
    }

    // Upload-date and duration presets are the ones YouTube itself offers; yt-dlp cannot filter by arbitrary ranges.
    public IReadOnlyList<FilterOption<UploadDateFilter>> UploadDateOptions { get; } =
    [
        new("Any time", UploadDateFilter.Any),
        new("Last hour", UploadDateFilter.LastHour),
        new("Today", UploadDateFilter.Today),
        new("This week", UploadDateFilter.ThisWeek),
        new("This month", UploadDateFilter.ThisMonth),
        new("This year", UploadDateFilter.ThisYear),
    ];

    public IReadOnlyList<FilterOption<DurationFilter>> DurationOptions { get; } =
    [
        new("Any length", DurationFilter.Any),
        new("Under 4 minutes", DurationFilter.UnderFourMinutes),
        new("4–20 minutes", DurationFilter.FourToTwentyMinutes),
        new("Over 20 minutes", DurationFilter.OverTwentyMinutes),
    ];

    public ObservableCollection<SearchResultViewModel> Results { get; }
    public ObservableCollection<ChannelResultViewModel> Channels { get; }
    public ObservableCollection<DownloadProfile> Profiles { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public bool IsChannelMode
    {
        get => _isChannelMode;
        set
        {
            if (!SetProperty(ref _isChannelMode, value))
                return;

            OnPropertyChanged(nameof(IsVideoMode));
            OnPropertyChanged(nameof(SearchPlaceholder));
            OnPropertyChanged(nameof(HasResults));
            SearchAgainIfSearched();
        }
    }

    public bool IsVideoMode
    {
        get => !_isChannelMode;
        set => IsChannelMode = !value;
    }

    public string SearchPlaceholder => IsChannelMode ? "Search YouTube channels" : "Search YouTube videos";

    public FilterOption<UploadDateFilter> SelectedUploadDate
    {
        get => _selectedUploadDate;
        set
        {
            if (value is not null && SetProperty(ref _selectedUploadDate, value))
                SearchAgainIfSearched();
        }
    }

    public FilterOption<DurationFilter> SelectedDuration
    {
        get => _selectedDuration;
        set
        {
            if (value is not null && SetProperty(ref _selectedDuration, value))
                SearchAgainIfSearched();
        }
    }

    public bool HideShorts
    {
        get => _hideShorts;
        set
        {
            if (SetProperty(ref _hideShorts, value))
                SearchAgainIfSearched();
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set => SetProperty(ref _isLoadingMore, value);
    }

    public bool CanLoadMore
    {
        get => _canLoadMore;
        private set => SetProperty(ref _canLoadMore, value);
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

    public bool HasResults => IsChannelMode ? Channels.Count > 0 : Results.Count > 0;

    public async Task InitializeAsync()
    {
        await RefreshProfilesAsync();
    }

    public async Task RefreshProfilesAsync()
    {
        var profiles = await ProfileListLoader.LoadOrderedAsync(_profileStore);
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);
    }

    public Task SearchAsync() => FetchResultsAsync(reset: true);

    public Task LoadMoreAsync() => FetchResultsAsync(reset: false);

    private void SearchAgainIfSearched()
    {
        if (_hasSearched && !string.IsNullOrWhiteSpace(SearchQuery))
            _ = SearchAsync();
    }

    private async Task FetchResultsAsync(bool reset)
    {
        CancellationToken token;
        if (reset)
        {
            ErrorMessage = null;
            StatusMessage = null;
            CancelThumbnailLoads();

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ErrorMessage = "Enter a search query.";
                return;
            }

            // Supersedes any search or load-more still in flight.
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            token = _searchCts.Token;

            _hasSearched = true;
            _activeQuery = SearchQuery.Trim();
            _activeChannelMode = IsChannelMode;
            _activeFilters = new SearchFilters(SelectedUploadDate.Value, SelectedDuration.Value, HideShorts);
            _nextSkip = 0;
            IsLoadingMore = false;
            IsSearching = true;
            Results.Clear();
            Channels.Clear();
            CanLoadMore = false;
            OnPropertyChanged(nameof(HasResults));
        }
        else
        {
            if (!CanLoadMore || IsSearching || IsLoadingMore || _searchCts is null)
                return;

            ErrorMessage = null;
            IsLoadingMore = true;
            token = _searchCts.Token;
        }

        try
        {
            var config = await _appConfigStore.LoadAsync(token);
            var defaultProfile = Profiles.FirstOrDefault(p => p.Id == config.ActiveProfileId)
                ?? Profiles.FirstOrDefault();
            var profileList = Profiles.ToList();

            List<ThumbnailRowViewModel> newRows;
            bool hasMore;
            var emptyPages = 0;
            do
            {
                (newRows, hasMore) = _activeChannelMode
                    ? await FetchChannelPageAsync(profileList, defaultProfile, token)
                    : await FetchVideoPageAsync(profileList, defaultProfile, token);
            }
            while (newRows.Count == 0 && hasMore && ++emptyPages < MaxEmptyPagesInARow && !token.IsCancellationRequested);

            if (token.IsCancellationRequested)
                return;

            CanLoadMore = hasMore;
            OnPropertyChanged(nameof(HasResults));
            UpdateStatusMessage();

            if (newRows.Count > 0)
            {
                if (reset)
                {
                    _thumbnailLoadCts?.Cancel();
                    _thumbnailLoadCts?.Dispose();
                    _thumbnailLoadCts = new CancellationTokenSource();
                }
                else
                {
                    _thumbnailLoadCts ??= new CancellationTokenSource();
                }

                _ = LoadThumbnailsAsync(newRows, _thumbnailLoadCts.Token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            // A cancelled call was superseded by a newer search that owns these flags now.
            if (!token.IsCancellationRequested)
            {
                if (reset)
                    IsSearching = false;
                else
                    IsLoadingMore = false;
            }
        }
    }

    private async Task<(List<ThumbnailRowViewModel> NewRows, bool HasMore)> FetchVideoPageAsync(
        List<DownloadProfile> profiles,
        DownloadProfile? defaultProfile,
        CancellationToken token)
    {
        var page = await _searchService.SearchAsync(_activeQuery, _activeFilters, _nextSkip, token);
        if (token.IsCancellationRequested)
            return ([], false);

        _nextSkip = page.NextSkip;
        var existingIds = Results.Select(r => r.Result.VideoId).ToHashSet(StringComparer.Ordinal);
        var newRows = new List<ThumbnailRowViewModel>();
        foreach (var result in page.Results.Where(r => existingIds.Add(r.VideoId)))
        {
            var row = new SearchResultViewModel(
                result,
                profiles,
                defaultProfile,
                _enqueueCoordinator,
                _thumbnailLoader);
            newRows.Add(row);
            Results.Add(row);
        }

        return (newRows, page.HasMoreResults);
    }

    private async Task<(List<ThumbnailRowViewModel> NewRows, bool HasMore)> FetchChannelPageAsync(
        List<DownloadProfile> profiles,
        DownloadProfile? defaultProfile,
        CancellationToken token)
    {
        var page = await _searchService.SearchChannelsAsync(_activeQuery, _nextSkip, token);
        var subscribedIds = (await _subscriptionMonitor.ListAsync(token))
            .Select(s => s.ChannelId)
            .ToHashSet(StringComparer.Ordinal);
        if (token.IsCancellationRequested)
            return ([], false);

        _nextSkip = page.NextSkip;
        var existingIds = Channels.Select(c => c.Result.ChannelId).ToHashSet(StringComparer.Ordinal);
        var newRows = new List<ThumbnailRowViewModel>();
        foreach (var result in page.Results.Where(r => existingIds.Add(r.ChannelId)))
        {
            var row = new ChannelResultViewModel(
                result,
                profiles,
                defaultProfile,
                subscribedIds.Contains(result.ChannelId),
                _subscriptionMonitor,
                _thumbnailLoader);
            newRows.Add(row);
            Channels.Add(row);
        }

        return (newRows, page.HasMoreResults);
    }

    private void UpdateStatusMessage()
    {
        var count = _activeChannelMode ? Channels.Count : Results.Count;
        var noun = _activeChannelMode ? "channel" : "video";
        if (count == 0)
        {
            StatusMessage = CanLoadMore ? null : $"No {noun}s found.";
            return;
        }

        StatusMessage = CanLoadMore
            ? $"{count} loaded"
            : count == 1 ? $"1 {noun} found." : $"{count} {noun}s found.";
    }

    private async Task LoadThumbnailsAsync(
        IReadOnlyList<ThumbnailRowViewModel> rows,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await row.LoadThumbnailAsync(cancellationToken);
        }
    }

    private void CancelThumbnailLoads()
    {
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        _thumbnailLoadCts = null;
    }
}
