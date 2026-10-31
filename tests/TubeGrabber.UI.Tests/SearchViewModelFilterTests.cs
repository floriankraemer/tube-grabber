// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SearchViewModelFilterTests
{
    private readonly IYtDlpSearchService _searchService = Substitute.For<IYtDlpSearchService>();
    private readonly ISubscriptionMonitor _monitor = Substitute.For<ISubscriptionMonitor>();
    private readonly SearchViewModel _vm;

    public SearchViewModelFilterTests()
    {
        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });
        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new DownloadProfile { Id = "default", Name = "Default" }]);
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        _vm = new SearchViewModel(
            _searchService,
            appConfig,
            profileStore,
            ViewModelTestHelpers.CreateEnqueueCoordinator(),
            Substitute.For<IThumbnailLoader>(),
            _monitor);
    }

    [Fact]
    public async Task Defaults_HideShortsOnAndNoOtherFilter()
    {
        ReturnVideoPage(skip: 0, nextSkip: 0, hasMore: false);
        _vm.SearchQuery = "cats";

        await _vm.SearchAsync();

        Assert.True(_vm.HideShorts);
        await _searchService.Received(1).SearchAsync(
            "cats",
            new SearchFilters(UploadDateFilter.Any, DurationFilter.Any, HideShorts: true),
            0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_PassesSelectedFilters()
    {
        ReturnVideoPage(skip: 0, nextSkip: 0, hasMore: false);
        _vm.SearchQuery = "cats";
        _vm.SelectedUploadDate = _vm.UploadDateOptions.Single(o => o.Value == UploadDateFilter.ThisWeek);
        _vm.SelectedDuration = _vm.DurationOptions.Single(o => o.Value == DurationFilter.OverTwentyMinutes);
        _vm.HideShorts = false;

        await _vm.SearchAsync();

        await _searchService.Received(1).SearchAsync(
            "cats",
            new SearchFilters(UploadDateFilter.ThisWeek, DurationFilter.OverTwentyMinutes, HideShorts: false),
            0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangingFilterAfterSearch_SearchesAgain()
    {
        ReturnVideoPage(skip: 0, nextSkip: 0, hasMore: false);
        _vm.SearchQuery = "cats";
        await _vm.SearchAsync();

        _vm.HideShorts = false;

        await WaitUntilAsync(() => _searchService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "SearchAsync") == 2);
        await _searchService.Received(1).SearchAsync(
            "cats",
            new SearchFilters(HideShorts: false),
            0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ChangingFilterBeforeAnySearch_DoesNotSearch()
    {
        _vm.SearchQuery = "cats";

        _vm.HideShorts = false;
        _vm.IsChannelMode = true;

        Assert.Empty(_searchService.ReceivedCalls());
    }

    [Fact]
    public async Task SearchAsync_PageFullyFilteredOut_ContinuesWithNextPage()
    {
        _searchService.SearchAsync("cats", Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage { Query = "cats", Results = [], NextSkip = 20, HasMoreResults = true });
        _searchService.SearchAsync("cats", Arg.Any<SearchFilters>(), 20, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = [Video("v1")],
                Skip = 20,
                NextSkip = 40,
                HasMoreResults = false,
            });
        _vm.SearchQuery = "cats";

        await _vm.SearchAsync();

        Assert.Equal(["v1"], _vm.Results.Select(r => r.Result.VideoId));
        Assert.False(_vm.CanLoadMore);
    }

    [Fact]
    public async Task LoadMoreAsync_UsesNextSkipInsteadOfVisibleRowCount()
    {
        _searchService.SearchAsync("cats", Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = [Video("v1")],
                NextSkip = 20,
                HasMoreResults = true,
            });
        _searchService.SearchAsync("cats", Arg.Any<SearchFilters>(), 20, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = [Video("v2")],
                Skip = 20,
                NextSkip = 40,
                HasMoreResults = false,
            });
        _vm.SearchQuery = "cats";
        await _vm.SearchAsync();

        await _vm.LoadMoreAsync();

        Assert.Equal(["v1", "v2"], _vm.Results.Select(r => r.Result.VideoId));
    }

    [Fact]
    public async Task ChannelMode_SearchesChannelsAndMarksSubscribed()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new ChannelSubscription { ChannelId = "UC1", ChannelName = "One", ChannelUrl = "u", ProfileId = "default" }]);
        _searchService.SearchChannelsAsync("science", 0, Arg.Any<CancellationToken>())
            .Returns(new ChannelSearchPage
            {
                Query = "science",
                Results =
                [
                    new ChannelSearchResult { ChannelId = "UC1", Name = "One", ChannelUrl = "https://www.youtube.com/channel/UC1" },
                    new ChannelSearchResult { ChannelId = "UC2", Name = "Two", ChannelUrl = "https://www.youtube.com/channel/UC2" },
                ],
                NextSkip = 2,
            });
        await _vm.InitializeAsync();
        _vm.IsChannelMode = true;
        _vm.SearchQuery = "science";

        await _vm.SearchAsync();

        Assert.Empty(_vm.Results);
        Assert.Equal([true, false], _vm.Channels.Select(c => c.IsSubscribed));
        Assert.True(_vm.HasResults);
        Assert.Equal("2 channels found.", _vm.StatusMessage);
        await _searchService.DidNotReceiveWithAnyArgs().SearchAsync(default!, default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SwitchingMode_ClearsOtherModeResults()
    {
        ReturnVideoPage(skip: 0, nextSkip: 1, hasMore: false, Video("v1"));
        _searchService.SearchChannelsAsync("cats", 0, Arg.Any<CancellationToken>())
            .Returns(new ChannelSearchPage { Query = "cats", Results = [] });
        _vm.SearchQuery = "cats";
        await _vm.SearchAsync();

        _vm.IsChannelMode = true;

        await WaitUntilAsync(() => _vm.StatusMessage == "No channels found.");
        Assert.Empty(_vm.Results);
        Assert.False(_vm.HasResults);
    }

    private void ReturnVideoPage(int skip, int nextSkip, bool hasMore, params YouTubeSearchResult[] results) =>
        _searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), skip, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = results,
                Skip = skip,
                NextSkip = nextSkip,
                HasMoreResults = hasMore,
            });

    private static YouTubeSearchResult Video(string id) => new()
    {
        VideoId = id,
        Title = id,
        WatchUrl = $"https://www.youtube.com/watch?v={id}",
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
