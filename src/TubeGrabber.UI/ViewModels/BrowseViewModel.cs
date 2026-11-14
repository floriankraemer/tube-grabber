// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

/// <summary>
/// Browse page: the subscription home feed as a tile grid, swapped for search results while a query is active.
/// A tile opens the watch view, which replaces the grid until <see cref="Back"/>.
/// </summary>
public sealed class BrowseViewModel : ViewModelBase
{
    public const int FeedVideosPerChannel = 6;

    private readonly IYtDlpSearchService _searchService;
    private readonly ISubscriptionFeedService _feedService;
    private readonly IThumbnailLoader _thumbnailLoader;

    private string _searchQuery = string.Empty;
    private string _activeQuery = string.Empty;
    private bool _isSearchMode;
    private bool _hasLoaded;
    private int _nextSkip;
    private bool _isLoading;
    private bool _isLoadingMore;
    private bool _canLoadMore;
    private string? _errorMessage;
    private object? _currentWatch;
    private List<VideoTileViewModel>? _feedTiles;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _thumbnailCts;

    public BrowseViewModel(
        IYtDlpSearchService searchService,
        ISubscriptionFeedService feedService,
        IThumbnailLoader thumbnailLoader)
    {
        _searchService = searchService;
        _feedService = feedService;
        _thumbnailLoader = thumbnailLoader;
        Tiles = new ObservableCollection<VideoTileViewModel>();
        Tiles.CollectionChanged += (_, _) => RaiseViewStateChanged();
    }

    /// <summary>Raised when the empty-feed hint asks to go to the Subscriptions page.</summary>
    public event EventHandler? SubscriptionsRequested;

    /// <summary>
    /// Builds the watch view model for a clicked video. The watch page replaces this default, which just
    /// uses the video itself as a placeholder so the grid hides.
    /// </summary>
    public Func<YouTubeSearchResult, object> WatchFactory { get; set; } = static result => result;

    public ObservableCollection<VideoTileViewModel> Tiles { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                OnPropertyChanged(nameof(CanClearSearch));
        }
    }

    /// <summary>Show the clear button while there is text or a search is displayed.</summary>
    public bool CanClearSearch => _isSearchMode || !string.IsNullOrEmpty(_searchQuery);

    /// <summary>Null shows the grid; anything else is the watch view model and hides the grid.</summary>
    public object? CurrentWatch
    {
        get => _currentWatch;
        private set
        {
            if (!SetProperty(ref _currentWatch, value))
                return;

            OnPropertyChanged(nameof(IsWatching));
            OnPropertyChanged(nameof(ShowGrid));
        }
    }

    public bool IsWatching => _currentWatch is not null;

    public bool ShowGrid => !IsWatching;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                RaiseViewStateChanged();
        }
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

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                RaiseViewStateChanged();
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasTiles => Tiles.Count > 0;

    public string PageSubtitle => _isSearchMode
        ? $"Results for “{_activeQuery}”"
        : "Latest videos from your subscriptions.";

    public string LoadingText => _isSearchMode ? "Searching…" : "Loading your feed…";

    /// <summary>First page in flight with nothing to show yet.</summary>
    public bool ShowInitialLoading => IsLoading && !HasTiles;

    public bool ShowEmptyFeed => _hasLoaded && !_isSearchMode && !IsLoading && !HasTiles && !HasError;

    public bool ShowNoResults => _hasLoaded && _isSearchMode && !IsLoading && !HasTiles && !HasError;

    private void RaiseViewStateChanged()
    {
        OnPropertyChanged(nameof(HasTiles));
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowEmptyFeed));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private void RaiseModeChanged()
    {
        OnPropertyChanged(nameof(CanClearSearch));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(LoadingText));
        RaiseViewStateChanged();
    }

    /// <summary>Loads the home feed the first time the page is shown.</summary>
    public Task InitializeAsync() => LoadFeedAsync();

    /// <summary>Retries whatever failed: the feed, or the active search.</summary>
    public Task RetryAsync() => _isSearchMode ? FetchSearchAsync(reset: true) : LoadFeedAsync();

    public void RequestSubscriptions() => SubscriptionsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Runs the typed query; an empty query returns to the feed.</summary>
    public Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return ClearSearchAsync();

        _activeQuery = SearchQuery.Trim();
        _isSearchMode = true;
        return FetchSearchAsync(reset: true);
    }

    public Task LoadMoreAsync() => FetchSearchAsync(reset: false);

    /// <summary>Leaves search mode and shows the feed again.</summary>
    public async Task ClearSearchAsync()
    {
        SearchQuery = string.Empty;
        if (!_isSearchMode)
            return;

        _isSearchMode = false;
        _activeQuery = string.Empty;
        CanLoadMore = false;
        ErrorMessage = null;
        if (_feedTiles is null)
        {
            await LoadFeedAsync();
            return;
        }

        CancelLoads();
        IsLoading = false;
        IsLoadingMore = false;
        ReplaceTiles(_feedTiles);
        RaiseModeChanged();
    }

    public void OpenWatch(YouTubeSearchResult result) => CurrentWatch = WatchFactory(result);

    public void Back()
    {
        var watch = CurrentWatch;
        CurrentWatch = null;
        (watch as IDisposable)?.Dispose();
    }

    private CancellationToken BeginLoad()
    {
        CancelLoads();
        _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    private void CancelLoads()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
    }

    private void ReplaceTiles(IEnumerable<VideoTileViewModel> tiles)
    {
        Tiles.Clear();
        foreach (var tile in tiles)
            Tiles.Add(tile);
    }

    private async Task LoadFeedAsync()
    {
        var token = BeginLoad();
        ErrorMessage = null;
        _isSearchMode = false;
        IsLoadingMore = false;
        CanLoadMore = false;
        _feedTiles = null;
        Tiles.Clear();
        IsLoading = true;
        RaiseModeChanged();

        try
        {
            var feed = await _feedService.GetHomeFeedAsync(FeedVideosPerChannel, token);
            if (token.IsCancellationRequested)
                return;

            _feedTiles = feed.Select(r => new VideoTileViewModel(r, _thumbnailLoader)).ToList();
            ReplaceTiles(_feedTiles);
            _hasLoaded = true;
            StartThumbnailLoads(_feedTiles, reset: true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _hasLoaded = true;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private async Task FetchSearchAsync(bool reset)
    {
        CancellationToken token;
        if (reset)
        {
            token = BeginLoad();
            ErrorMessage = null;
            _nextSkip = 0;
            IsLoadingMore = false;
            CanLoadMore = false;
            Tiles.Clear();
            IsLoading = true;
            RaiseModeChanged();
        }
        else
        {
            if (!_isSearchMode || !CanLoadMore || IsLoading || IsLoadingMore || _loadCts is null)
                return;

            ErrorMessage = null;
            IsLoadingMore = true;
            token = _loadCts.Token;
        }

        try
        {
            var page = await _searchService.SearchAsync(_activeQuery, SearchFilters.Default, _nextSkip, token);
            if (token.IsCancellationRequested)
                return;

            _nextSkip = page.NextSkip;
            var known = Tiles.Select(t => t.Result.VideoId).ToHashSet(StringComparer.Ordinal);
            var added = page.Results
                .Where(r => known.Add(r.VideoId))
                .Select(r => new VideoTileViewModel(r, _thumbnailLoader))
                .ToList();
            foreach (var tile in added)
                Tiles.Add(tile);

            CanLoadMore = page.HasMoreResults;
            _hasLoaded = true;
            StartThumbnailLoads(added, reset);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _hasLoaded = true;
        }
        finally
        {
            // A cancelled call was superseded by a newer one that owns these flags now.
            if (!token.IsCancellationRequested)
            {
                IsLoading = false;
                IsLoadingMore = false;
            }
        }
    }

    private void StartThumbnailLoads(IReadOnlyList<VideoTileViewModel> tiles, bool reset)
    {
        if (tiles.Count == 0)
            return;

        if (reset)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = new CancellationTokenSource();
        }

        _thumbnailCts ??= new CancellationTokenSource();
        _ = LoadThumbnailsAsync(tiles, _thumbnailCts.Token);
    }

    private static async Task LoadThumbnailsAsync(IReadOnlyList<VideoTileViewModel> tiles, CancellationToken token)
    {
        foreach (var tile in tiles)
        {
            if (token.IsCancellationRequested)
                return;

            await tile.LoadThumbnailAsync(token);
        }
    }
}
