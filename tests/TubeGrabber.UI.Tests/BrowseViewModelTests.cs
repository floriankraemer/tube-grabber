// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class BrowseViewModelTests
{
    private readonly IYtDlpSearchService _search = Substitute.For<IYtDlpSearchService>();
    private readonly ISubscriptionFeedService _feed = Substitute.For<ISubscriptionFeedService>();

    private readonly ISubscriptionMonitor _subscriptions = Substitute.For<ISubscriptionMonitor>();

    private BrowseViewModel CreateVm() => new(_search, _feed, Substitute.For<IThumbnailLoader>(), _subscriptions);

    private void Subscribed(params string[] channels) =>
        _subscriptions.ListAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<IReadOnlyList<ChannelSubscription>>(channels.Select(c => new ChannelSubscription
            {
                ChannelId = c,
                ChannelName = c,
                ChannelUrl = $"https://www.youtube.com/@{c}",
                ProfileId = "p",
            }).ToList()));

    private static YouTubeSearchResult Video(string id, string? channel = "Chan") => new()
    {
        VideoId = id,
        Title = $"Title {id}",
        Channel = channel,
        WatchUrl = $"https://www.youtube.com/watch?v={id}",
        DurationSeconds = 65,
    };

    private void FeedReturns(params YouTubeSearchResult[] videos) =>
        _feed.GetHomeFeedAsync(BrowseViewModel.FeedVideosPerChannel, Arg.Any<CancellationToken>()).Returns(videos);

    private void SearchReturns(int skip, bool hasMore, int nextSkip, params YouTubeSearchResult[] videos) =>
        _search.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), skip, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage { Query = "q", Results = videos, HasMoreResults = hasMore, NextSkip = nextSkip });

    [Fact]
    public async Task InitializeAsync_LoadsFeedTiles()
    {
        FeedReturns(Video("a"), Video("b", channel: null));
        var vm = CreateVm();

        await vm.InitializeAsync();

        Assert.Equal(["a", "b"], vm.Tiles.Select(t => t.Result.VideoId));
        Assert.Equal("1:05", vm.Tiles[0].DurationDisplay);
        Assert.Equal(string.Empty, vm.Tiles[1].Channel);
        Assert.True(vm.HasTiles);
        Assert.False(vm.IsLoading);
        Assert.False(vm.ShowEmptyFeed);
        Assert.False(vm.CanLoadMore);
        Assert.Equal("Latest videos from your subscriptions.", vm.PageSubtitle);
    }

    [Fact]
    public async Task InitializeAsync_ShowsLoadingWhileFeedInFlight()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<YouTubeSearchResult>>();
        _feed.GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = CreateVm();

        var load = vm.InitializeAsync();

        Assert.True(vm.ShowInitialLoading);
        Assert.Equal("Loading your feed…", vm.LoadingText);
        gate.SetResult([Video("a")]);
        await load;
        Assert.False(vm.ShowInitialLoading);
    }

    [Fact]
    public async Task InitializeAsync_EmptyFeed_ShowsEmptyState()
    {
        FeedReturns();
        var vm = CreateVm();

        await vm.InitializeAsync();

        Assert.True(vm.ShowEmptyFeed);
        Assert.False(vm.ShowNoResults);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task InitializeAsync_Failure_ShowsErrorAndRetryRecovers()
    {
        _feed.GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<YouTubeSearchResult>>(new InvalidOperationException("boom")));
        var vm = CreateVm();

        await vm.InitializeAsync();

        Assert.True(vm.HasError);
        Assert.Equal("boom", vm.ErrorMessage);
        Assert.False(vm.ShowEmptyFeed);

        FeedReturns(Video("a"));
        await vm.RetryAsync();

        Assert.False(vm.HasError);
        Assert.Single(vm.Tiles);
    }

    [Fact]
    public async Task SearchAsync_ReplacesFeedWithResults()
    {
        FeedReturns(Video("feed"));
        SearchReturns(0, hasMore: true, nextSkip: 20, Video("s1"), Video("s2"));
        var vm = CreateVm();
        await vm.InitializeAsync();

        vm.SearchQuery = "  cats ";
        await vm.SearchAsync();

        Assert.Equal(["s1", "s2"], vm.Tiles.Select(t => t.Result.VideoId));
        Assert.True(vm.CanLoadMore);
        Assert.True(vm.CanClearSearch);
        Assert.Equal("Results for “cats”", vm.PageSubtitle);
        await _search.Received(1).SearchAsync("cats", SearchFilters.Default, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_NoRows_ShowsNoResults()
    {
        FeedReturns(Video("feed"));
        SearchReturns(0, hasMore: false, nextSkip: 0);
        var vm = CreateVm();
        await vm.InitializeAsync();

        vm.SearchQuery = "zzz";
        await vm.SearchAsync();

        Assert.True(vm.ShowNoResults);
        Assert.False(vm.ShowEmptyFeed);
        Assert.Equal("Searching…", vm.LoadingText);
    }

    [Fact]
    public async Task SearchAsync_Failure_ShowsErrorAndRetrySearches()
    {
        FeedReturns(Video("feed"));
        _search.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("offline"));
        var vm = CreateVm();
        await vm.InitializeAsync();

        vm.SearchQuery = "cats";
        await vm.SearchAsync();
        Assert.Equal("offline", vm.ErrorMessage);

        SearchReturns(0, hasMore: false, nextSkip: 1, Video("s1"));
        await vm.RetryAsync();

        Assert.False(vm.HasError);
        Assert.Equal(["s1"], vm.Tiles.Select(t => t.Result.VideoId));
    }

    [Fact]
    public async Task SearchAsync_BlankQuery_ReturnsToFeed()
    {
        FeedReturns(Video("feed"));
        SearchReturns(0, hasMore: false, nextSkip: 1, Video("s1"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        vm.SearchQuery = "   ";
        await vm.SearchAsync();

        Assert.Equal(["feed"], vm.Tiles.Select(t => t.Result.VideoId));
        Assert.Equal(string.Empty, vm.SearchQuery);
    }

    [Fact]
    public async Task ClearSearchAsync_RestoresCachedFeedWithoutRefetch()
    {
        FeedReturns(Video("feed"));
        SearchReturns(0, hasMore: true, nextSkip: 1, Video("s1"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        await vm.ClearSearchAsync();

        Assert.Equal(["feed"], vm.Tiles.Select(t => t.Result.VideoId));
        Assert.False(vm.CanLoadMore);
        Assert.False(vm.CanClearSearch);
        Assert.Equal(string.Empty, vm.SearchQuery);
        await _feed.Received(1).GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearSearchAsync_WithoutCachedFeed_LoadsFeed()
    {
        _feed.GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<IReadOnlyList<YouTubeSearchResult>>(new InvalidOperationException("boom")),
                Task.FromResult<IReadOnlyList<YouTubeSearchResult>>([Video("feed")]));
        SearchReturns(0, hasMore: false, nextSkip: 1, Video("s1"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        await vm.ClearSearchAsync();

        Assert.Equal(["feed"], vm.Tiles.Select(t => t.Result.VideoId));
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task ClearSearchAsync_OutsideSearch_OnlyClearsText()
    {
        FeedReturns(Video("feed"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SearchQuery = "typed";
        Assert.True(vm.CanClearSearch);

        await vm.ClearSearchAsync();

        Assert.Equal(string.Empty, vm.SearchQuery);
        await _feed.Received(1).GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadMoreAsync_AppendsAndSkipsDuplicates()
    {
        FeedReturns();
        SearchReturns(0, hasMore: true, nextSkip: 20, Video("s1"));
        SearchReturns(20, hasMore: false, nextSkip: 40, Video("s1"), Video("s2"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        await vm.LoadMoreAsync();

        Assert.Equal(["s1", "s2"], vm.Tiles.Select(t => t.Result.VideoId));
        Assert.False(vm.CanLoadMore);
        Assert.False(vm.IsLoadingMore);
    }

    [Fact]
    public async Task LoadMoreAsync_InFeedMode_IsNoOp()
    {
        FeedReturns(Video("a"));
        var vm = CreateVm();
        await vm.InitializeAsync();

        await vm.LoadMoreAsync();

        await _search.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OpenWatch_DefaultFactory_SetsCurrentWatchAndHidesGrid()
    {
        var vm = CreateVm();
        var video = Video("a");

        vm.OpenWatch(video);

        Assert.Same(video, vm.CurrentWatch);
        Assert.True(vm.IsWatching);
        Assert.False(vm.ShowGrid);
    }

    [Fact]
    public void OpenWatch_UsesWatchFactoryAndBackDisposes()
    {
        var vm = CreateVm();
        var watch = Substitute.For<IDisposable>();
        vm.WatchFactory = _ => watch;

        vm.OpenWatch(Video("a"));
        Assert.Same(watch, vm.CurrentWatch);

        vm.Back();

        Assert.Null(vm.CurrentWatch);
        Assert.True(vm.ShowGrid);
        watch.Received(1).Dispose();
    }

    [Fact]
    public void OpenWatch_Twice_DisposesPreviousWatchBeforeCreatingNext()
    {
        var vm = CreateVm();
        var first = Substitute.For<IDisposable>();
        var second = Substitute.For<IDisposable>();
        var queue = new Queue<IDisposable>([first, second]);
        vm.WatchFactory = _ =>
        {
            first.Received(queue.Count == 1 ? 1 : 0).Dispose();
            return queue.Dequeue();
        };

        vm.OpenWatch(Video("a"));
        vm.OpenWatch(Video("b"));

        Assert.Same(second, vm.CurrentWatch);
        first.Received(1).Dispose();
        second.DidNotReceive().Dispose();
    }

    [Fact]
    public void RequestSubscriptions_RaisesEvent()
    {
        var vm = CreateVm();
        var raised = false;
        vm.SubscriptionsRequested += (_, _) => raised = true;

        vm.RequestSubscriptions();

        Assert.True(raised);
    }

    [Fact]
    public async Task RefreshIfSubscriptionsChangedAsync_NewSubscription_ReloadsTheFeed()
    {
        Subscribed("a");
        FeedReturns(Video("old"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        Subscribed("a", "b");
        FeedReturns(Video("old"), Video("new"));

        await vm.RefreshIfSubscriptionsChangedAsync();

        Assert.Equal(["old", "new"], vm.Tiles.Select(t => t.Result.VideoId));
    }

    [Fact]
    public async Task RefreshIfSubscriptionsChangedAsync_SameChannels_KeepsTheFeed()
    {
        Subscribed("a", "b");
        FeedReturns(Video("x"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        Subscribed("b", "a");

        await vm.RefreshIfSubscriptionsChangedAsync();

        await _feed.Received(1).GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshIfSubscriptionsChangedAsync_BeforeTheFirstLoad_DoesNothing()
    {
        Subscribed("a");
        var vm = CreateVm();

        await vm.RefreshIfSubscriptionsChangedAsync();

        await _feed.DidNotReceive().GetHomeFeedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshIfSubscriptionsChangedAsync_WhileSearching_ReloadsTheFeedOnClear()
    {
        Subscribed("a");
        FeedReturns(Video("old"));
        SearchReturns(0, hasMore: false, nextSkip: 1, Video("s1"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();
        Subscribed("a", "b");
        FeedReturns(Video("fresh"));

        await vm.RefreshIfSubscriptionsChangedAsync();
        Assert.Equal(["s1"], vm.Tiles.Select(t => t.Result.VideoId));

        await vm.ClearSearchAsync();

        Assert.Equal(["fresh"], vm.Tiles.Select(t => t.Result.VideoId));
    }

    [Fact]
    public async Task RefreshIfSubscriptionsChangedAsync_SubscriptionsUnreadable_KeepsTheFeed()
    {
        Subscribed("a");
        FeedReturns(Video("x"));
        var vm = CreateVm();
        await vm.InitializeAsync();
        _subscriptions.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("locked"));

        await vm.RefreshIfSubscriptionsChangedAsync();

        Assert.Equal(["x"], vm.Tiles.Select(t => t.Result.VideoId));
    }

    [Fact]
    public async Task ContentReset_RaisedWhenFeedLoadsSearchStartsAndSearchClears()
    {
        FeedReturns(Video("f"));
        SearchReturns(0, hasMore: false, nextSkip: 1, Video("s1"));
        var vm = CreateVm();
        var resets = 0;
        vm.ContentReset += (_, _) => resets++;

        await vm.InitializeAsync();
        Assert.Equal(1, resets);

        vm.SearchQuery = "cats";
        await vm.SearchAsync();
        Assert.Equal(2, resets);

        await vm.ClearSearchAsync();
        Assert.Equal(3, resets);
    }
}
