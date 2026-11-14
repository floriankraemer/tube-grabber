// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class WatchViewModelTests : IDisposable
{
    private const string WatchUrl = "https://www.youtube.com/watch?v=v1";

    private readonly IVideoDetailsService _details = Substitute.For<IVideoDetailsService>();
    private readonly IVideoCommentsService _comments = Substitute.For<IVideoCommentsService>();
    private readonly ISponsorBlockClient _sponsorBlock = Substitute.For<ISponsorBlockClient>();
    private readonly IAppConfigStore _config = Substitute.For<IAppConfigStore>();
    private readonly IProfileStore _profiles = Substitute.For<IProfileStore>();
    private readonly IDownloadQueueService _queue = Substitute.For<IDownloadQueueService>();
    private readonly FakePlayer _player = new();
    private readonly AppConfiguration _appConfig = new() { ActiveProfileId = "b", DownloadFolder = "/tmp", SponsorBlockEnabled = true };
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tube-grabber-watch-vm", Guid.NewGuid().ToString("N"));

    public WatchViewModelTests()
    {
        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>()).Returns(Details());
        _comments.GetTopCommentsAsync(WatchUrl, Arg.Any<CancellationToken>()).Returns(Array.Empty<VideoComment>());
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>()).Returns(Array.Empty<SponsorSegment>());
        _config.LoadAsync(Arg.Any<CancellationToken>()).Returns(_appConfig);
        var a = new DownloadProfile { Id = "a", Name = "Alpha" };
        var b = new DownloadProfile { Id = "b", Name = "Beta" };
        _profiles.ListAsync(Arg.Any<CancellationToken>()).Returns([a, b]);
        _profiles.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new DownloadProfile { Id = call.Arg<string>(), Name = "x" });
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Fact]
    public async Task LoadAsync_PlaysPickedStreamsAndShowsDetails()
    {
        using var vm = Create();

        await vm.LoadAsync();

        Assert.Equal([("video-url", "audio-url")], _player.Played);
        Assert.Equal("Full title", vm.Title);
        Assert.Contains("1,234 views", vm.StatsLine, StringComparison.Ordinal);
        Assert.False(vm.IsLoadingDetails);
        Assert.False(vm.HasDetailsError);
        Assert.True(vm.IsPlaybackAvailable);
    }

    [Fact]
    public async Task LoadAsync_DetailsFail_ShowsErrorAndDoesNotPlay()
    {
        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        using var vm = Create();

        await vm.LoadAsync();

        Assert.Equal("boom", vm.DetailsError);
        Assert.Empty(_player.Played);
        Assert.False(vm.IsLoadingDetails);
    }

    [Fact]
    public async Task LoadAsync_RetryAfterError_Recovers()
    {
        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<VideoDetails>(new InvalidOperationException("boom")), _ => Task.FromResult(Details()));
        using var vm = Create();

        await vm.LoadAsync();
        await vm.LoadAsync();

        Assert.False(vm.HasDetailsError);
        Assert.Single(_player.Played);
    }

    [Fact]
    public async Task LoadAsync_NoStreamUrl_ReportsPlaybackError()
    {
        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(new VideoDetails { Id = "v1", Title = "T" });
        using var vm = Create();

        await vm.LoadAsync();

        Assert.True(vm.HasPlaybackError);
        Assert.Empty(_player.Played);
    }

    [Fact]
    public async Task LoadAsync_NoPlayer_MarksPlaybackUnavailableButKeepsDetails()
    {
        using var vm = Create(withPlayer: false);

        await vm.LoadAsync();

        Assert.False(vm.IsPlaybackAvailable);
        Assert.False(vm.ShowPlaybackSpinner);
        Assert.Equal("Full title", vm.Title);
        Assert.Null(vm.Player);
        vm.TogglePlayPause();
        vm.CommitSeek();
        vm.Volume = 20;
    }

    [Fact]
    public async Task Comments_Loaded_AreListedWithMetadata()
    {
        _comments.GetTopCommentsAsync(WatchUrl, Arg.Any<CancellationToken>()).Returns(new[]
        {
            new VideoComment { Id = "1", Author = "Ann", Text = "Hi", LikeCount = 1200, PostedAt = DateTimeOffset.UtcNow.AddDays(-3), IsPinned = true, AuthorIsUploader = true },
        });
        using var vm = Create();

        await vm.LoadAsync();

        var comment = Assert.Single(vm.Comments);
        Assert.Equal("Ann", comment.Author);
        Assert.Equal("3 days ago", comment.PostedAgo);
        Assert.Equal($"{1200L:N0} likes", comment.Likes);
        Assert.True(comment.IsPinned && comment.IsUploader);
        Assert.Equal("Comments (1)", vm.CommentsHeader);
        Assert.True(vm.HasComments);
        Assert.False(vm.ShowNoComments);
    }

    [Fact]
    public async Task Comments_Empty_ShowsEmptyState()
    {
        using var vm = Create();

        await vm.LoadAsync();

        Assert.True(vm.ShowNoComments);
        Assert.Equal("Comments", vm.CommentsHeader);
    }

    [Fact]
    public async Task Comments_ErrorThenRetry_Recovers()
    {
        _comments.GetTopCommentsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<IReadOnlyList<VideoComment>>(new InvalidOperationException("slow")),
                _ => Task.FromResult<IReadOnlyList<VideoComment>>([new VideoComment { Id = "1", Author = "A", Text = "t" }]));
        using var vm = Create();

        await vm.LoadAsync();
        Assert.Equal("slow", vm.CommentsError);
        Assert.False(vm.IsLoadingComments);
        Assert.False(vm.ShowNoComments);

        await vm.RetryCommentsAsync();

        Assert.False(vm.HasCommentsError);
        Assert.Single(vm.Comments);
    }

    [Fact]
    public async Task Profiles_ActiveProfileIsListedFirst()
    {
        using var vm = Create();

        await vm.LoadAsync();

        Assert.Equal(["b", "a"], vm.Profiles.Select(p => p.Id));
    }

    [Fact]
    public async Task DownloadAsync_EnqueuesWatchUrlWithPickedProfile()
    {
        _queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DownloadJob { Url = WatchUrl, ProfileId = "a" });
        using var vm = Create();
        await vm.LoadAsync();

        await vm.DownloadAsync(vm.Profiles.Single(p => p.Id == "a"));

        await _queue.Received(1).EnqueueAsync(WatchUrl, "a", Arg.Any<CancellationToken>());
        Assert.True(vm.DownloadSucceeded);
        Assert.Equal("Added to downloads.", vm.DownloadMessage);
        Assert.False(vm.IsDownloading);
    }

    [Fact]
    public async Task DownloadAsync_Failure_SurfacesError()
    {
        _appConfig.DownloadFolder = string.Empty;
        using var vm = Create();
        await vm.LoadAsync();

        await vm.DownloadAsync(vm.Profiles[0]);

        Assert.False(vm.DownloadSucceeded);
        Assert.Contains("folder", vm.DownloadMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.HasDownloadMessage);
    }

    [Fact]
    public async Task SponsorBlock_PlaybackEnteringSegment_SkipsOnce()
    {
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>())
            .Returns(new[] { new SponsorSegment("u1", "sponsor", 10, 20) });
        using var vm = Create();
        vm.SkipNoticeDuration = TimeSpan.FromMilliseconds(500);
        await vm.LoadAsync();

        _player.RaisePosition(9.5);
        _player.RaisePosition(10.2);
        _player.RaisePosition(11);
        _player.RaisePosition(15);

        Assert.Equal([20d], _player.Seeks);
        Assert.True(vm.HasSegments);
        Assert.Equal("Skipped sponsor segment", vm.SkipNotice);
        Assert.True(vm.HasSkipNotice);
        for (var i = 0; i < 100 && vm.HasSkipNotice; i++)
            await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Null(vm.SkipNotice);
    }

    [Fact]
    public async Task SponsorBlock_SeekingIntoTheMiddleOfASegment_DoesNotSkip()
    {
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>())
            .Returns(new[] { new SponsorSegment("u1", "sponsor", 10, 20) });
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaisePosition(2);
        _player.RaisePosition(15);
        _player.RaisePosition(15.3);

        Assert.Empty(_player.Seeks);
        Assert.False(vm.HasSkipNotice);
    }

    [Fact]
    public async Task SponsorBlock_PlayingIntoSegmentFromBeforeItsStartAfterSeekingBack_SkipsAgain()
    {
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>())
            .Returns(new[] { new SponsorSegment("u1", "sponsor", 10, 20) });
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaisePosition(9.8);
        _player.RaisePosition(10.1);
        _player.RaisePosition(20.2);
        _player.RaisePosition(40);
        _player.RaisePosition(5);
        _player.RaisePosition(9.9);
        _player.RaisePosition(10.2);

        Assert.Equal([20d, 20d], _player.Seeks);
    }

    [Fact]
    public async Task SponsorBlock_SeekingBackIntoASkippedSegment_LeavesItAlone()
    {
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>())
            .Returns(new[] { new SponsorSegment("u1", "sponsor", 10, 20) });
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaisePosition(9.8);
        _player.RaisePosition(10.1);
        _player.RaisePosition(25);
        _player.RaisePosition(14);
        _player.RaisePosition(14.3);

        Assert.Equal([20d], _player.Seeks);
    }

    [Fact]
    public async Task SponsorBlock_SegmentAtTheVeryStart_IsSkipped()
    {
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>())
            .Returns(new[] { new SponsorSegment("u1", "intro", 0, 8) });
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaisePosition(0.3);

        Assert.Equal([8d], _player.Seeks);
    }

    [Fact]
    public async Task Buffering_FollowsThePlayer()
    {
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaiseBuffering(true);
        Assert.True(vm.IsBuffering);

        _player.RaiseBuffering(false);
        Assert.False(vm.IsBuffering);
    }

    [Fact]
    public async Task SponsorBlock_Disabled_NeverFetchesOrSeeks()
    {
        _appConfig.SponsorBlockEnabled = false;
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaisePosition(11);

        Assert.Empty(_player.Seeks);
        await _sponsorBlock.DidNotReceive().GetSkipSegmentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SponsorBlock_LookupThrows_PlaybackStillStarts()
    {
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("offline"));
        using var vm = Create();

        await vm.LoadAsync();

        Assert.Single(_player.Played);
        Assert.Empty(vm.Segments);
    }

    [Fact]
    public async Task Position_UpdatesTimeText_ButNotSeekValueWhileScrubbing()
    {
        using var vm = Create();
        await vm.LoadAsync();
        _player.RaiseLength(125);

        _player.RaisePosition(65);
        Assert.Equal("1:05 / 2:05", vm.TimeText);
        Assert.Equal(65, vm.SeekValue);

        vm.BeginScrub();
        _player.RaisePosition(70);
        Assert.Equal(65, vm.SeekValue);

        vm.SeekValue = 100;
        vm.CommitSeek();
        Assert.Equal([100d], _player.Seeks);
        Assert.True(vm.CanSeek);
    }

    [Fact]
    public async Task Player_Controls_AreForwarded()
    {
        using var vm = Create();
        await vm.LoadAsync();

        _player.RaisePlaying(true);
        vm.TogglePlayPause();
        _player.RaisePlaying(false);
        vm.TogglePlayPause();
        vm.Volume = 150;

        Assert.Equal(["pause", "resume"], _player.Commands);
        Assert.Equal(100, vm.Volume);
        Assert.Equal(100, _player.Volume);
        _player.RaiseFailed("bad");
        Assert.Equal("bad", vm.PlaybackError);
        _player.RaiseLength(0);
    }

    [Fact]
    public async Task Description_ToggleAndExpandability()
    {
        using var vm = Create();
        await vm.LoadAsync();
        Assert.False(vm.CanExpandDescription);

        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>()).Returns(Details(description: new string('x', 300)));
        await vm.LoadAsync();

        Assert.True(vm.CanExpandDescription);
        vm.ToggleDescription();
        Assert.Equal("Show less", vm.DescriptionToggleText);
        vm.IsFullscreen = true;
        Assert.True(vm.IsFullscreen);
    }

    [Fact]
    public async Task Dispose_CancelsLoadingAndDisposesPlayerOnce()
    {
        var gate = new TaskCompletionSource<VideoDetails>();
        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = Create();
        var load = vm.LoadAsync();

        vm.Dispose();
        vm.Dispose();
        gate.SetResult(Details());
        await load;

        Assert.Equal(1, _player.DisposeCount);
        Assert.Empty(_player.Played);
        _player.RaisePosition(3);
        await vm.LoadAsync();
        Assert.Empty(_player.Played);
    }

    [Fact]
    public async Task Dispose_CancelsPendingSegmentLookup()
    {
        CancellationToken seen = default;
        _sponsorBlock.GetSkipSegmentsAsync("v1", Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                seen = call.Arg<CancellationToken>();
                return Task.Delay(Timeout.Infinite, seen).ContinueWith(_ => (IReadOnlyList<SponsorSegment>)[], CancellationToken.None);
            });
        var vm = Create();
        var load = vm.LoadAsync();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        vm.Dispose();
        await load;

        Assert.True(seen.IsCancellationRequested);
    }

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(300, "5 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(86400 * 40, "1 month ago")]
    [InlineData(86400 * 800, "2 years ago")]
    public void RelativeTime_FormatsUnits(int seconds, string expected)
    {
        Assert.Equal(expected, CommentViewModel.RelativeTime(TimeSpan.FromSeconds(seconds)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Comments_UnreadableOutput_ShowsErrorWithRetry()
    {
        var parser = new YtDlpCommentsParser();
        _comments.GetTopCommentsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(parser.Parse(string.Empty)),
                _ => Task.FromResult<IReadOnlyList<VideoComment>>([new VideoComment { Id = "1", Author = "A", Text = "t" }]));
        using var vm = Create();

        await vm.LoadAsync();

        Assert.True(vm.HasCommentsError);
        Assert.False(vm.ShowNoComments);

        await vm.RetryCommentsAsync();

        Assert.False(vm.HasCommentsError);
        Assert.Single(vm.Comments);
    }

    [Fact]
    public async Task Comments_TimeOut_ShowsTimeoutErrorAndPlaybackStillStarts()
    {
        _comments.GetTopCommentsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(call => HangUntilCancelled<IReadOnlyList<VideoComment>>(call.Arg<CancellationToken>()));
        using var vm = Create();
        vm.CommentsTimeout = TimeSpan.FromMilliseconds(50);

        await vm.LoadAsync();

        Assert.Contains("Loading comments took longer than", vm.CommentsError, StringComparison.Ordinal);
        Assert.False(vm.IsLoadingComments);
        Assert.Equal([("video-url", "audio-url")], _player.Played);
    }

    [Fact]
    public async Task Details_TimeOut_ShowsTimeoutErrorAndRetryRecovers()
    {
        _details.GetDetailsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(
                call => HangUntilCancelled<VideoDetails>(call.Arg<CancellationToken>()),
                _ => Task.FromResult(Details()));
        using var vm = Create();
        vm.DetailsTimeout = TimeSpan.FromMilliseconds(50);

        await vm.LoadAsync();

        Assert.Contains("Loading the video took longer than", vm.DetailsError, StringComparison.Ordinal);
        Assert.False(vm.IsLoadingDetails);

        await vm.LoadAsync();

        Assert.False(vm.HasDetailsError);
        Assert.Single(_player.Played);
    }

    [Fact]
    public async Task RetryCommentsAsync_TwiceRapidly_KeepsOnlyTheLatestResult()
    {
        var stale = new TaskCompletionSource<IReadOnlyList<VideoComment>>();
        _comments.GetTopCommentsAsync(WatchUrl, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult<IReadOnlyList<VideoComment>>([]),
                _ => stale.Task,
                _ => Task.FromResult<IReadOnlyList<VideoComment>>([new VideoComment { Id = "new", Author = "A", Text = "t" }]));
        using var vm = Create();
        await vm.LoadAsync();

        var first = vm.RetryCommentsAsync();
        await vm.RetryCommentsAsync();
        stale.SetResult([new VideoComment { Id = "old1", Author = "A", Text = "t" }, new VideoComment { Id = "old2", Author = "A", Text = "t" }]);
        await first;

        Assert.Equal(["t"], vm.Comments.Select(c => c.Text));
        Assert.False(vm.IsLoadingComments);
    }

    [Fact]
    public async Task RefreshProfilesAsync_TwiceRapidly_YieldsOneConsistentList()
    {
        using var vm = Create();
        await vm.LoadAsync();

        await Task.WhenAll(vm.RefreshProfilesAsync(), vm.RefreshProfilesAsync());

        Assert.Equal(["b", "a"], vm.Profiles.Select(p => p.Id));
    }

    private static async Task<T> HangUntilCancelled<T>(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token);
        return default!;
    }

    private WatchViewModel Create(bool withPlayer = true)
    {
        var coordinator = new DownloadEnqueueCoordinator(
            _queue, _config, _profiles, new YouTubeUrlNormalizer(), new BinaryLocator(_root), new DownloadFolderService());
        return new WatchViewModel(
            new YouTubeSearchResult { VideoId = "v1", Title = "Result title", Channel = "Chan", WatchUrl = WatchUrl },
            _details, _comments, _sponsorBlock, coordinator, _config, _profiles,
            withPlayer ? _player : null);
    }

    private static VideoDetails Details(string? description = "short") => new()
    {
        Id = "v1",
        Title = "Full title",
        Channel = "Chan",
        ViewCount = 1234,
        UploadDate = new DateOnly(2025, 3, 3),
        DurationSeconds = 120,
        Description = description,
        VideoStreamUrl = "video-url",
        AudioStreamUrl = "audio-url",
    };

    private sealed class FakePlayer : IVideoPlayer
    {
        public List<(string Video, string? Audio)> Played { get; } = [];
        public List<double> Seeks { get; } = [];
        public List<string> Commands { get; } = [];
        public int DisposeCount { get; private set; }
        public int Volume { get; set; } = 100;

        public event Action<double>? PositionChanged;
        public event Action<double>? LengthChanged;
        public event Action<bool>? IsPlayingChanged;
        public event Action<bool>? BufferingChanged;
        public event Action<string>? Failed;

        public void Play(string videoUrl, string? audioUrl) => Played.Add((videoUrl, audioUrl));
        public void Pause() => Commands.Add("pause");
        public void Resume() => Commands.Add("resume");
        public void Seek(double seconds) => Seeks.Add(seconds);
        public void Dispose() => DisposeCount++;
        public void RaisePosition(double s) => PositionChanged?.Invoke(s);
        public void RaiseLength(double s) => LengthChanged?.Invoke(s);
        public void RaisePlaying(bool p) => IsPlayingChanged?.Invoke(p);
        public void RaiseBuffering(bool b) => BufferingChanged?.Invoke(b);
        public void RaiseFailed(string m) => Failed?.Invoke(m);
    }
}
