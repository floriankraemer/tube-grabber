// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class SubscriptionMonitorTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeFeed _feed = new();
    private readonly FakeQueue _queue = new();
    private readonly FakeStore _store = new();
    private readonly FakeNotifier _notifier = new();
    private readonly AppConfiguration _config;
    private readonly SubscriptionMonitor _monitor;

    public SubscriptionMonitorTests()
    {
        _config = new AppConfiguration
        {
            ActiveProfileId = "default",
            DownloadFolder = "/tmp/downloads",
            SubscriptionCheckIntervalMinutes = 15,
        };
        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _config);

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.GetAsync("default", Arg.Any<CancellationToken>())
            .Returns(new DownloadProfile { Id = "default", Name = "Default" });

        var root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-monitor-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);

        var coordinator = new DownloadEnqueueCoordinator(
            _queue,
            appConfig,
            profileStore,
            new YouTubeUrlNormalizer(),
            new BinaryLocator(root),
            new DownloadFolderService());

        _monitor = new SubscriptionMonitor(_store, _feed, coordinator, _queue, appConfig, _notifier, _time);
    }

    public async ValueTask DisposeAsync() => await _monitor.DisposeAsync();

    [Fact]
    public async Task AddAsync_WithBacklog_EnqueuesNewestNOldestFirst()
    {
        _feed.Entries = Videos("v5", "v4", "v3", "v2", "v1");

        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 2);

        Assert.Equal([Watch("v4"), Watch("v5")], _queue.EnqueuedUrls);
        Assert.Equal("https://www.youtube.com/channel/UC1/videos", subscription.ChannelUrl);
        Assert.Equal(["v3", "v2", "v1"], subscription.SeenVideoIds);
        Assert.Equal(2, _store.Saved.Count);
        Assert.NotNull(subscription.LastCheckedAt);
    }

    [Fact]
    public async Task AddAsync_ZeroBacklog_EnqueuesNothing()
    {
        _feed.Entries = Videos("v2", "v1");

        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);

        Assert.Empty(_queue.EnqueuedUrls);
        Assert.Equal(["v2", "v1"], subscription.SeenVideoIds);
    }

    [Fact]
    public async Task AddAsync_BacklogAboveScanWindow_IsClamped()
    {
        _feed.Entries = Videos("v2", "v1");

        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 500);

        Assert.Equal([Watch("v1"), Watch("v2")], _queue.EnqueuedUrls);
    }

    [Fact]
    public async Task AddAsync_DuplicateChannel_Throws()
    {
        _feed.Entries = Videos("v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0));

        Assert.Equal("You are already subscribed to Channel One.", ex.Message);
        Assert.Single(await _monitor.ListAsync());
    }

    [Fact]
    public async Task CheckNowAsync_EnqueuesOnlyUnseenVideos()
    {
        _feed.Entries = Videos("v2", "v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);

        _feed.Entries = Videos("v3", "v2", "v1");
        await _monitor.CheckNowAsync();

        Assert.Equal([Watch("v3")], _queue.EnqueuedUrls);
    }

    [Fact]
    public async Task CheckNowAsync_VideoInFlight_IsNotEnqueuedTwice()
    {
        _feed.Entries = Videos("v2", "v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);

        await _monitor.CheckNowAsync();
        await _monitor.CheckNowAsync();

        Assert.Equal([Watch("v2")], _queue.EnqueuedUrls);
    }

    [Fact]
    public async Task CompletedJob_MarksSeenAndNotifies()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);
        var job = _queue.Jobs.Single();
        job.Title = "Real Title";
        job.Status = DownloadStatus.Completed;

        _queue.RaiseJobsChanged();

        var (title, body) = await _notifier.NextAsync();
        Assert.Equal("New video downloaded", title);
        Assert.Equal("Channel One: Real Title", body);
        Assert.Contains("v1", subscription.SeenVideoIds);

        await _monitor.CheckNowAsync();
        Assert.Single(_queue.EnqueuedUrls);
    }

    [Fact]
    public async Task FailedJob_MarksSeenAndNotifiesFailure()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);
        _queue.Jobs.Single().Status = DownloadStatus.Failed;

        _queue.RaiseJobsChanged();

        var (title, body) = await _notifier.NextAsync();
        Assert.Equal("Download failed", title);
        Assert.Equal("Channel One: Title v1", body);
        Assert.Contains("v1", subscription.SeenVideoIds);
    }

    [Fact]
    public async Task RemovedJob_MarksSeenWithoutNotification()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);
        _queue.Clear();

        _queue.RaiseJobsChanged();

        await Eventually(() => subscription.SeenVideoIds.Contains("v1"));
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task CancelledJob_MarksSeenWithoutNotification()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);
        _queue.Jobs.Single().Status = DownloadStatus.Cancelled;

        _queue.RaiseJobsChanged();

        await Eventually(() => subscription.SeenVideoIds.Contains("v1"));
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task RunningJob_StaysTracked()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);
        _queue.Jobs.Single().Status = DownloadStatus.Running;

        _queue.RaiseJobsChanged();
        await _monitor.CheckNowAsync();

        Assert.DoesNotContain("v1", subscription.SeenVideoIds);
        Assert.Single(_queue.EnqueuedUrls);
    }

    [Fact]
    public async Task NotifierFailure_IsSwallowed()
    {
        _notifier.Throw = true;
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 1);
        _queue.Jobs.Single().Status = DownloadStatus.Completed;
        var changed = new TaskCompletionSource();
        _monitor.SubscriptionsChanged += (_, _) =>
        {
            if (subscription.SeenVideoIds.Contains("v1"))
                changed.TrySetResult();
        };

        _queue.RaiseJobsChanged();

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(_notifier.ThrowCount > 0);
    }

    [Fact]
    public async Task CheckNowAsync_DisabledSubscription_IsSkipped()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);
        await _monitor.SetEnabledAsync(subscription.Id, false);

        _feed.Entries = Videos("v2", "v1");
        await _monitor.CheckNowAsync();

        Assert.Empty(_queue.EnqueuedUrls);
        Assert.False((await _monitor.ListAsync()).Single().Enabled);
    }

    [Fact]
    public async Task CheckNowAsync_FeedError_SetsLastErrorAndRecovers()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);

        _feed.Error = new InvalidOperationException("boom");
        await _monitor.CheckNowAsync();
        Assert.Equal("boom", subscription.LastError);

        _feed.Error = null;
        await _monitor.CheckNowAsync();
        Assert.Null(subscription.LastError);
    }

    [Fact]
    public async Task CheckNowAsync_UnknownProfile_ReportsEnqueueError()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);
        await _monitor.SetProfileAsync(subscription.Id, "gone");

        _feed.Entries = Videos("v2", "v1");
        await _monitor.CheckNowAsync();

        Assert.Contains("gone", subscription.LastError);
        Assert.Empty(_queue.EnqueuedUrls);
    }

    [Fact]
    public async Task RemoveAsync_DeletesSubscription()
    {
        _feed.Entries = Videos("v1");
        var subscription = await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);

        await _monitor.RemoveAsync(subscription.Id);
        await _monitor.RemoveAsync("missing");

        Assert.Empty(await _monitor.ListAsync());
        Assert.Empty(_store.Saved.Last());
    }

    [Fact]
    public async Task ListAsync_LoadsPersistedSubscriptions()
    {
        _store.Saved.Add([new ChannelSubscription
        {
            ChannelId = "UC9",
            ChannelName = "Persisted",
            ChannelUrl = "https://example.invalid",
            ProfileId = "default",
        }]);

        Assert.Equal("Persisted", (await _monitor.ListAsync()).Single().ChannelName);
    }

    [Fact]
    public async Task Start_ScansImmediatelyAndAgainAfterInterval_HonoringChangedInterval()
    {
        _feed.Entries = Videos("v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);
        var callsAfterAdd = _feed.Calls;
        _feed.OnCall = () => _config.SubscriptionCheckIntervalMinutes = 30;

        _monitor.Start();
        await Eventually(() => _feed.Calls > callsAfterAdd);

        var minutes = 0;
        await Eventually(() =>
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            minutes++;
            return _feed.Calls > callsAfterAdd + 1;
        });

        Assert.True(minutes >= 30, $"second scan after {minutes} minutes");
    }

    [Fact]
    public async Task Start_ClampsIntervalToMinimum()
    {
        _config.SubscriptionCheckIntervalMinutes = 1;
        _feed.Entries = Videos("v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);
        var callsAfterAdd = _feed.Calls;
        _monitor.Start();
        await Eventually(() => _feed.Calls > callsAfterAdd);

        var minutes = 0;
        await Eventually(() =>
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            minutes++;
            return _feed.Calls > callsAfterAdd + 1;
        });

        Assert.True(minutes >= 15, $"second scan after {minutes} minutes");
    }

    [Fact]
    public async Task Start_LoopSurvivesExceptions()
    {
        _feed.Entries = Videos("v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);
        var callsAfterAdd = _feed.Calls;
        _store.FailSaves = true;

        _monitor.Start();
        await Eventually(() => _store.FailedSaves > 0);
        _store.FailSaves = false;

        await Eventually(() =>
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            return _feed.Calls > callsAfterAdd + 1;
        });
    }

    [Fact]
    public async Task DisposeAsync_StopsLoop()
    {
        _feed.Entries = Videos("v1");
        await _monitor.AddAsync("https://www.youtube.com/@chan", "default", 0);
        var callsAfterAdd = _feed.Calls;
        _monitor.Start();
        await Eventually(() => _feed.Calls > callsAfterAdd);

        await _monitor.DisposeAsync();
        var calls = _feed.Calls;
        _time.Advance(TimeSpan.FromHours(48));

        Assert.Equal(calls, _feed.Calls);
    }

    private static string Watch(string id) => $"https://www.youtube.com/watch?v={id}";

    private static List<YouTubeSearchResult> Videos(params string[] idsNewestFirst) =>
        idsNewestFirst
            .Select(id => new YouTubeSearchResult { VideoId = id, Title = $"Title {id}", WatchUrl = Watch(id) })
            .ToList();

    private static async Task Eventually(Func<bool> condition, bool allowTimeout = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(5);
        }

        if (!allowTimeout)
            Assert.Fail("Condition was not met in time.");
    }

    private sealed class FakeFeed : IChannelFeedService
    {
        private int _calls;

        public IReadOnlyList<YouTubeSearchResult> Entries { get; set; } = [];
        public Exception? Error { get; set; }
        public Action? OnCall { get; set; }
        public int Calls => Volatile.Read(ref _calls);

        public Task<ChannelFeed> GetLatestAsync(string channelUrl, int count, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            OnCall?.Invoke();
            if (Error is not null)
                throw Error;

            return Task.FromResult(new ChannelFeed { ChannelId = "UC1", ChannelName = "Channel One", Entries = Entries });
        }
    }

    private sealed class FakeStore : ISubscriptionStore
    {
        public List<IReadOnlyList<ChannelSubscription>> Saved { get; } = [];
        public bool FailSaves { get; set; }
        public int FailedSaves { get; private set; }

        public Task<IReadOnlyList<ChannelSubscription>> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(Saved.Count == 0 ? [] : Saved[^1]);

        public Task SaveAsync(IReadOnlyList<ChannelSubscription> subscriptions, CancellationToken ct = default)
        {
            if (FailSaves)
            {
                FailedSaves++;
                throw new IOException("disk full");
            }

            Saved.Add(subscriptions.ToList());
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotifier : IDesktopNotifier
    {
        private readonly TaskCompletionSource<(string, string)> _first = new();

        public List<(string Title, string Body)> Shown { get; } = [];
        public bool Throw { get; set; }
        public int ThrowCount { get; private set; }

        public Task<(string Title, string Body)> NextAsync() => _first.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public Task ShowAsync(string title, string body, CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                ThrowCount++;
                throw new InvalidOperationException("no tray");
            }

            Shown.Add((title, body));
            _first.TrySetResult((title, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueue : IDownloadQueueService
    {
        private readonly List<DownloadJob> _jobs = [];

        public List<string> EnqueuedUrls { get; } = [];
        public IReadOnlyList<DownloadJob> Jobs
        {
            get
            {
                lock (_jobs)
                    return _jobs.ToList();
            }
        }

        public event EventHandler? JobsChanged;

        public void RaiseJobsChanged() => JobsChanged?.Invoke(this, EventArgs.Empty);

        public void Clear()
        {
            lock (_jobs)
                _jobs.Clear();
        }

        public Task<DownloadJob> EnqueueAsync(string url, string profileId, CancellationToken cancellationToken = default)
        {
            var job = new DownloadJob { Url = url, ProfileId = profileId };
            lock (_jobs)
                _jobs.Add(job);
            EnqueuedUrls.Add(url);
            return Task.FromResult(job);
        }

        public Task StartJobAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
