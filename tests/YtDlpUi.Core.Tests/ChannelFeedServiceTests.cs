// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class ChannelFeedServiceTests : IDisposable
{
    private const string FeedJson = """
        {
          "channel_id": "UCabc",
          "channel": "Chan",
          "entries": [
            {"id": "aaaaaaaaaaa", "title": "Live now", "live_status": "is_live"},
            {"id": "bbbbbbbbbbb", "title": "Normal", "live_status": "not_live"},
            {"id": "ccccccccccc", "title": "Members", "availability": "subscriber_only"},
            {"id": "ddddddddddd", "title": "Soon", "live_status": "is_upcoming"},
            {"id": "eeeeeeeeeee", "title": "Public", "availability": "public"},
            {"id": "fffffffffff", "title": "Was live", "live_status": "post_live"},
            {"id": "ggggggggggg", "title": "Premium", "availability": "premium_only"},
            {"id": "hhhhhhhhhhh", "title": "Auth", "availability": "needs_auth"}
          ]
        }
        """;

    private readonly string _root;

    public ChannelFeedServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-channel-feed-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Theory]
    [InlineData("https://www.youtube.com/@chan", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://youtube.com/@chan/", "https://www.youtube.com/@chan/videos")]
    [InlineData("http://www.youtube.com/@chan", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://m.youtube.com/@chan", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://www.youtube.com/@chan/videos", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://www.youtube.com/@chan/shorts", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://www.youtube.com/@chan/streams", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://www.youtube.com/@chan/featured?view=0", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://www.youtube.com/@chan?si=abc#frag", "https://www.youtube.com/@chan/videos")]
    [InlineData("https://www.youtube.com/channel/UC123", "https://www.youtube.com/channel/UC123/videos")]
    [InlineData("https://www.youtube.com/channel/UC123/playlists", "https://www.youtube.com/channel/UC123/videos")]
    [InlineData("https://www.youtube.com/c/name", "https://www.youtube.com/c/name/videos")]
    [InlineData("https://www.youtube.com/user/name/videos", "https://www.youtube.com/user/name/videos")]
    [InlineData("@chan", "https://www.youtube.com/@chan/videos")]
    [InlineData("  @chan  ", "https://www.youtube.com/@chan/videos")]
    public void ToVideosTabUrl_NormalizesChannelForms(string input, string expected) =>
        Assert.Equal(expected, ChannelFeedService.ToVideosTabUrl(input));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://www.youtube.com/playlist?list=PL1")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/channel")]
    [InlineData("https://www.youtube.com/@")]
    [InlineData("https://www.youtube.com/shorts/abc")]
    public void ToVideosTabUrl_NonChannelUrl_Throws(string input)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ChannelFeedService.ToVideosTabUrl(input));

        Assert.Equal("Enter a YouTube channel URL, e.g. https://www.youtube.com/@channel.", ex.Message);
    }

    [Theory]
    [InlineData("", "URL cannot be empty.")]
    [InlineData("   ", "URL cannot be empty.")]
    [InlineData("not a url", "URL is not valid.")]
    [InlineData("ftp://www.youtube.com/@chan", "Only http and https URLs are supported.")]
    [InlineData("https://example.com/@chan", "Only youtube.com channel URLs are supported.")]
    [InlineData("https://youtu.be/abc", "Only youtube.com channel URLs are supported.")]
    public void ToVideosTabUrl_InvalidInput_Throws(string input, string message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ChannelFeedService.ToVideosTabUrl(input));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public async Task GetLatestAsync_FiltersLiveAndRestrictedEntriesPreservingOrder()
    {
        var (service, _) = await CreateServiceAsync(new CapturingProcessRunner(0, FeedJson));

        var feed = await service.GetLatestAsync("https://www.youtube.com/@chan", 10, TestContext.Current.CancellationToken);

        Assert.Equal("UCabc", feed.ChannelId);
        Assert.Equal("Chan", feed.ChannelName);
        Assert.Equal(["bbbbbbbbbbb", "eeeeeeeeeee"], feed.Entries.Select(e => e.VideoId));
    }

    [Fact]
    public async Task GetLatestAsync_PassesVideosUrlAndRangeToRunner()
    {
        var (service, process) = await CreateServiceAsync(new CapturingProcessRunner(0, FeedJson));

        await service.GetLatestAsync("https://www.youtube.com/@chan/shorts?x=1", 7, TestContext.Current.CancellationToken);

        var args = process.Invocation!.Arguments.ToList();
        Assert.Equal("1", args[args.IndexOf("--playlist-start") + 1]);
        Assert.Equal("7", args[args.IndexOf("--playlist-end") + 1]);
        Assert.Equal("https://www.youtube.com/@chan/videos", args[^1]);
    }

    [Fact]
    public async Task GetLatestAsync_UnparsableOutput_Throws()
    {
        var (service, _) = await CreateServiceAsync(new CapturingProcessRunner(0, "not json"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetLatestAsync("@chan", 5, TestContext.Current.CancellationToken));

        Assert.Equal("Could not read the channel feed from yt-dlp output.", ex.Message);
    }

    [Fact]
    public async Task GetLatestAsync_RunnerFailure_Propagates()
    {
        var (service, _) = await CreateServiceAsync(new CapturingProcessRunner(1, string.Empty));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetLatestAsync("@chan", 5, TestContext.Current.CancellationToken));

        Assert.Equal("yt-dlp exited with code 1.", ex.Message);
    }

    [Fact]
    public async Task GetLatestAsync_InvalidCount_Throws()
    {
        var (service, _) = await CreateServiceAsync(new CapturingProcessRunner(0, FeedJson));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetLatestAsync("@chan", 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatestAsync_NonChannelUrl_ThrowsBeforeRunning()
    {
        var (service, process) = await CreateServiceAsync(new CapturingProcessRunner(0, FeedJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetLatestAsync("https://www.youtube.com/watch?v=abc", 5, TestContext.Current.CancellationToken));

        Assert.Null(process.Invocation);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task<(ChannelFeedService Service, CapturingProcessRunner Process)> CreateServiceAsync(
        CapturingProcessRunner process)
    {
        var appConfig = new AppConfigStore(_root, new ProfileStore(_root));
        await appConfig.EnsureBootstrapAsync();
        var config = await appConfig.LoadAsync();
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config);

        var runner = new YtDlpFlatPlaylistRunner(process, appConfig, new BinaryLocator(_root), new JsRuntimeLocator());
        return (new ChannelFeedService(runner, new YouTubeUrlNormalizer()), process);
    }

    private sealed class CapturingProcessRunner(int exitCode, string stdout) : IYtDlpProcessRunner
    {
        public YtDlpInvocation? Invocation { get; private set; }

        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default)
        {
            Invocation = invocation;
            return Task.FromResult(new YtDlpRunResult { ExitCode = exitCode, StandardOutput = stdout });
        }
    }
}
