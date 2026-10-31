// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpSearchServiceTests : IDisposable
{
    private readonly string _root;

    public YtDlpSearchServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-search-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Fact]
    public async Task SearchAsync_ParsesRunnerOutput()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "vid1",
                  "title": "Test Video",
                  "channel": "Test Channel",
                  "webpage_url": "https://www.youtube.com/watch?v=vid1"
                }
              ]
            }
            """;

        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
        await appConfig.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        var service = new YtDlpSearchService(
            new SearchRunner(json),
            appConfig,
            new BinaryLocator(_root),
            new JsRuntimeLocator());

        var page = await service.SearchAsync("test query", SearchFilters.Default, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("test query", page.Query);
        Assert.Single(page.Results);
        Assert.Equal("Test Video", page.Results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_Throws()
    {
        var service = new YtDlpSearchService(
            new SearchRunner("{}"),
            new AppConfigStore(_root, new ProfileStore(_root)),
            new BinaryLocator(_root),
            new JsRuntimeLocator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchAsync("   ", SearchFilters.Default, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void BuildSearchArguments_FirstPage_UsesPlaylistRange()
    {
        var args = YtDlpSearchService.BuildSearchArguments(
            new AppConfiguration(),
            new JsRuntimeLocator(),
            "hello world");

        Assert.Contains("--playlist-start", args);
        Assert.Contains("1", args);
        Assert.Contains("--playlist-end", args);
        Assert.Contains("20", args);
        Assert.Equal("https://www.youtube.com/results?search_query=hello+world&sp=EgIQAQ%3D%3D", args[^1]);
    }

    [Fact]
    public void BuildSearchArguments_SecondPage_OffsetsPlaylistRange()
    {
        var args = YtDlpSearchService.BuildSearchArguments(
            new AppConfiguration(),
            new JsRuntimeLocator(),
            "hello world",
            skip: 20);

        Assert.Contains("--playlist-start", args);
        Assert.Contains("21", args);
        Assert.Contains("--playlist-end", args);
        Assert.Contains("40", args);
        Assert.Equal("https://www.youtube.com/results?search_query=hello+world&sp=EgIQAQ%3D%3D", args[^1]);
    }

    [Fact]
    public async Task SearchAsync_FullPage_SetsHasMoreResults()
    {
        var entryLines = Enumerable.Range(1, 20)
            .Select(i => $$"""{ "id": "vid{{i}}", "title": "Video {{i}}" }""");
        var json = $$"""
            { "entries": [ {{string.Join(", ", entryLines)}} ] }
            """;

        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
        await appConfig.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        var service = new YtDlpSearchService(
            new SearchRunner(json),
            appConfig,
            new BinaryLocator(_root),
            new JsRuntimeLocator());

        var page = await service.SearchAsync("query", SearchFilters.Default, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(20, page.Results.Count);
        Assert.True(page.HasMoreResults);
    }

    [Fact]
    public async Task SearchAsync_HideShorts_DropsShortVideosButCountsThemForPaging()
    {
        const string json = """
            { "entries": [
              { "id": "short", "title": "Short", "duration": 45 },
              { "id": "edge", "title": "Edge", "duration": 60 },
              { "id": "long", "title": "Long", "duration": 61 },
              { "id": "live", "title": "Live" }
            ] }
            """;
        var service = await CreateServiceAsync(json);

        var page = await service.SearchAsync("q", new SearchFilters(HideShorts: true), skip: 20, TestContext.Current.CancellationToken);

        Assert.Equal(["long", "live"], page.Results.Select(r => r.VideoId));
        Assert.Equal(24, page.NextSkip);
    }

    [Fact]
    public async Task SearchAsync_ShowShorts_KeepsEverything()
    {
        const string json = """
            { "entries": [ { "id": "short", "title": "Short", "duration": 45 } ] }
            """;
        var service = await CreateServiceAsync(json);

        var page = await service.SearchAsync("q", new SearchFilters(HideShorts: false), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(page.Results);
    }

    [Fact]
    public async Task SearchChannelsAsync_ParsesChannelsAndAdvancesSkip()
    {
        const string json = """
            { "entries": [ { "id": "UCone", "title": "One" }, { "id": "UCtwo", "title": "Two" } ] }
            """;
        var service = await CreateServiceAsync(json);

        var page = await service.SearchChannelsAsync("one", skip: 20, TestContext.Current.CancellationToken);

        Assert.Equal(["UCone", "UCtwo"], page.Results.Select(r => r.ChannelId));
        Assert.Equal(22, page.NextSkip);
        Assert.False(page.HasMoreResults);
    }

    [Fact]
    public async Task SearchChannelsAsync_EmptyQuery_Throws()
    {
        var service = await CreateServiceAsync("{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchChannelsAsync(" ", cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<YtDlpSearchService> CreateServiceAsync(string json)
    {
        var appConfig = new AppConfigStore(_root, new ProfileStore(_root));
        await appConfig.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        return new YtDlpSearchService(
            new SearchRunner(json),
            appConfig,
            new BinaryLocator(_root),
            new JsRuntimeLocator());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class SearchRunner(string output) : IYtDlpProcessRunner
    {
        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new YtDlpRunResult
            {
                ExitCode = 0,
                StandardOutput = output,
            });
    }
}
