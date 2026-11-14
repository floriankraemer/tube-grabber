// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class SubscriptionFeedServiceTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private readonly ISubscriptionStore _store = Substitute.For<ISubscriptionStore>();
    private readonly IChannelFeedService _channels = Substitute.For<IChannelFeedService>();
    private readonly SubscriptionFeedService _service;

    public SubscriptionFeedServiceTests()
    {
        _service = new SubscriptionFeedService(_store, _channels);
    }

    [Fact]
    public async Task GetHomeFeedAsync_InterleavesChannelsRoundRobinInSubscriptionOrder()
    {
        Subscribe(Sub("A"), Sub("B"), Sub("C"));
        Feed("A", "a1", "a2", "a3");
        Feed("B", "b1", "b2");
        Feed("C", "c1");

        var result = await _service.GetHomeFeedAsync(3, TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "b1", "c1", "a2", "b2", "a3"], result.Select(v => v.VideoId));
    }

    [Fact]
    public async Task GetHomeFeedAsync_KeepsSubscriptionOrderWhenChannelsCompleteOutOfOrder()
    {
        Subscribe(Sub("A"), Sub("B"));
        Feed("B", "b1");
        _channels.GetLatestAsync(Url("A"), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return FeedOf("A", "a1");
            });

        var result = await _service.GetHomeFeedAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "b1"], result.Select(v => v.VideoId));
    }

    [Fact]
    public async Task GetHomeFeedAsync_DedupesByVideoIdKeepingFirstOccurrence()
    {
        Subscribe(Sub("A"), Sub("B"));
        Feed("A", "shared", "a2");
        Feed("B", "shared", "b2", "b2");

        var result = await _service.GetHomeFeedAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(["shared", "a2", "b2"], result.Select(v => v.VideoId));
    }

    [Fact]
    public async Task GetHomeFeedAsync_ChannelThatThrows_IsSkipped()
    {
        Subscribe(Sub("A"), Sub("B"), Sub("C"));
        Feed("A", "a1");
        _channels.GetLatestAsync(Url("B"), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChannelFeed>>(_ => throw new InvalidOperationException("boom"));
        Feed("C", "c1");

        var result = await _service.GetHomeFeedAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "c1"], result.Select(v => v.VideoId));
    }

    [Fact]
    public async Task GetHomeFeedAsync_CancellationPropagates()
    {
        Subscribe(Sub("A"));
        var entered = new TaskCompletionSource();
        _channels.GetLatestAsync(Url("A"), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(2));
                return FeedOf("A");
            });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var pending = _service.GetHomeFeedAsync(1, cts.Token);
        await entered.Task.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task GetHomeFeedAsync_NoSubscriptions_ReturnsEmptyWithoutFetching()
    {
        Subscribe();

        var result = await _service.GetHomeFeedAsync(5, TestContext.Current.CancellationToken);

        Assert.Empty(result);
        await _channels.DidNotReceiveWithAnyArgs().GetLatestAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetHomeFeedAsync_DisabledSubscriptions_AreNotFetched()
    {
        Subscribe(Sub("A"), Sub("Paused", enabled: false));
        Feed("A", "a1");

        var result = await _service.GetHomeFeedAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(["a1"], result.Select(v => v.VideoId));
        await _channels.DidNotReceive().GetLatestAsync(Url("Paused"), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHomeFeedAsync_PassesPerChannelCountToChannelFeed()
    {
        Subscribe(Sub("A"));
        Feed("A", "a1");

        await _service.GetHomeFeedAsync(7, TestContext.Current.CancellationToken);

        await _channels.Received(1).GetLatestAsync(Url("A"), 7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetHomeFeedAsync_NonPositivePerChannel_Throws(int perChannel)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetHomeFeedAsync(perChannel, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHomeFeedAsync_FetchesAtMostFourChannelsConcurrently()
    {
        var names = Enumerable.Range(1, 10).Select(i => $"C{i}").ToArray();
        Subscribe(names.Select(n => Sub(n)).ToArray());

        var inFlight = 0;
        var maxInFlight = 0;
        var fourEntered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var entered = 0;
        var gate = new object();

        _channels.GetLatestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                lock (gate)
                {
                    inFlight++;
                    maxInFlight = Math.Max(maxInFlight, inFlight);
                    if (++entered == SubscriptionFeedService.MaxConcurrentChannels)
                        fourEntered.TrySetResult();
                }

                await release.Task;

                lock (gate)
                    inFlight--;

                return FeedOf("Channel", ci.ArgAt<string>(0));
            });

        var pending = _service.GetHomeFeedAsync(1, TestContext.Current.CancellationToken);
        await fourEntered.Task.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        release.TrySetResult();
        var result = await pending;

        Assert.Equal(SubscriptionFeedService.MaxConcurrentChannels, maxInFlight);
        Assert.Equal(names.Length, result.Count);
        await _channels.Received(names.Length).GetLatestAsync(Arg.Any<string>(), 1, Arg.Any<CancellationToken>());
    }

    private void Subscribe(params ChannelSubscription[] subscriptions) =>
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChannelSubscription>>(subscriptions));

    private void Feed(string name, params string[] videoIds) =>
        _channels.GetLatestAsync(Url(name), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(FeedOf(name, videoIds));

    private static ChannelSubscription Sub(string name, bool enabled = true) => new()
    {
        ChannelId = $"UC{name}",
        ChannelName = name,
        ChannelUrl = Url(name),
        ProfileId = "default",
        Enabled = enabled,
    };

    private static string Url(string name) => $"https://www.youtube.com/@{name}/videos";

    private static ChannelFeed FeedOf(string name, params string[] videoIds) => new()
    {
        ChannelId = $"UC{name}",
        ChannelName = name,
        Entries = videoIds.Select(id => new YouTubeSearchResult
        {
            VideoId = id,
            Title = id,
            WatchUrl = $"https://www.youtube.com/watch?v={id}",
        }).ToList(),
    };
}
