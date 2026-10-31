// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SearchViewModelTests
{
    [Fact]
    public async Task SearchAsync_PopulatesResults()
    {
        var searchService = Substitute.For<IYtDlpSearchService>();
        searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results =
                [
                    new YouTubeSearchResult
                    {
                        VideoId = "v1",
                        Title = "Cat Video",
                        Channel = "Cat Channel",
                        WatchUrl = "https://www.youtube.com/watch?v=v1",
                    },
                ],
                HasMoreResults = false,
            });

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new DownloadProfile { Id = "default", Name = "Default" }]);

        var vm = new SearchViewModel(
            searchService,
            appConfig,
            profileStore,
            CreateEnqueueCoordinator(),
            Substitute.For<IThumbnailLoader>(),
            Substitute.For<ISubscriptionMonitor>());

        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        Assert.True(vm.HasResults);
        Assert.Single(vm.Results);
        Assert.Equal("Cat Video", vm.Results[0].Title);
        Assert.Equal("1 video found.", vm.StatusMessage);
        Assert.False(vm.CanLoadMore);
        Assert.False(vm.ShowIntro);
        Assert.False(vm.ShowNoResults);
        Assert.False(vm.ShowInitialLoading);
    }

    [Fact]
    public async Task SearchAsync_WithoutRows_ShowsNoResults()
    {
        var searchService = Substitute.For<IYtDlpSearchService>();
        searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage { Query = "zzz", Results = [], HasMoreResults = false });

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppConfiguration());

        var vm = new SearchViewModel(
            searchService,
            appConfig,
            Substitute.For<IProfileStore>(),
            CreateEnqueueCoordinator(),
            Substitute.For<IThumbnailLoader>(),
            Substitute.For<ISubscriptionMonitor>());

        Assert.True(vm.ShowIntro);

        vm.SearchQuery = "zzz";
        await vm.SearchAsync();

        Assert.True(vm.ShowNoResults);
        Assert.False(vm.ShowIntro);
    }

    [Fact]
    public async Task LoadMoreAsync_AppendsNextPage()
    {
        var searchService = Substitute.For<IYtDlpSearchService>();
        searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = CreateResults("v", 1, 20),
                NextSkip = 20,
                HasMoreResults = true,
            });
        searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), 20, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = CreateResults("v", 21, 20),
                Skip = 20,
                NextSkip = 40,
                HasMoreResults = true,
            });

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new DownloadProfile { Id = "default", Name = "Default" }]);

        var vm = new SearchViewModel(
            searchService,
            appConfig,
            profileStore,
            CreateEnqueueCoordinator(),
            Substitute.For<IThumbnailLoader>(),
            Substitute.For<ISubscriptionMonitor>());

        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        Assert.Equal(20, vm.Results.Count);
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreAsync();

        Assert.Equal(40, vm.Results.Count);
        Assert.Equal("40 loaded", vm.StatusMessage);
        await searchService.Received(1).SearchAsync("cats", Arg.Any<SearchFilters>(), 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_FullPage_ShowsLoadedCount()
    {
        var searchService = Substitute.For<IYtDlpSearchService>();
        searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), 0, Arg.Any<CancellationToken>())
            .Returns(new SearchResultPage
            {
                Query = "cats",
                Results = CreateResults("v", 1, 20),
                NextSkip = 20,
                HasMoreResults = true,
            });

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new DownloadProfile { Id = "default", Name = "Default" }]);

        var vm = new SearchViewModel(
            searchService,
            appConfig,
            profileStore,
            CreateEnqueueCoordinator(),
            Substitute.For<IThumbnailLoader>(),
            Substitute.For<ISubscriptionMonitor>());

        await vm.InitializeAsync();
        vm.SearchQuery = "cats";
        await vm.SearchAsync();

        Assert.Equal(20, vm.Results.Count);
        Assert.True(vm.CanLoadMore);
        Assert.Equal("20 loaded", vm.StatusMessage);
    }

    [Fact]
    public async Task SearchAsync_ServiceError_SetsErrorMessage()
    {
        var searchService = Substitute.For<IYtDlpSearchService>();
        searchService.SearchAsync(Arg.Any<string>(), Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<SearchResultPage>>(_ => throw new InvalidOperationException("Search failed"));

        var vm = new SearchViewModel(
            searchService,
            Substitute.For<IAppConfigStore>(),
            Substitute.For<IProfileStore>(),
            CreateEnqueueCoordinator(),
            Substitute.For<IThumbnailLoader>(),
            Substitute.For<ISubscriptionMonitor>());

        vm.SearchQuery = "test";
        await vm.SearchAsync();

        Assert.Equal("Search failed", vm.ErrorMessage);
        Assert.False(vm.HasResults);
    }

    private static List<YouTubeSearchResult> CreateResults(string idPrefix, int start, int count)
    {
        var results = new List<YouTubeSearchResult>();
        for (var i = 0; i < count; i++)
        {
            var n = start + i;
            results.Add(new YouTubeSearchResult
            {
                VideoId = $"{idPrefix}{n}",
                Title = $"Video {n}",
                WatchUrl = $"https://www.youtube.com/watch?v={idPrefix}{n}",
            });
        }

        return results;
    }

    private static DownloadEnqueueCoordinator CreateEnqueueCoordinator() =>
        new(
            Substitute.For<IDownloadQueueService>(),
            Substitute.For<IAppConfigStore>(),
            Substitute.For<IProfileStore>(),
            new YouTubeUrlNormalizer(),
            new BinaryLocator(Path.GetTempPath()),
            new DownloadFolderService());
}
