// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class ChannelResultViewModelTests
{
    private static readonly DownloadProfile Profile = new() { Id = "p1", Name = "Profile" };
    private readonly ISubscriptionMonitor _monitor = Substitute.For<ISubscriptionMonitor>();

    [Fact]
    public async Task SubscribeAsync_SubscribesWithoutBacklog()
    {
        var row = CreateRow();

        await row.SubscribeAsync();

        await _monitor.Received(1).AddAsync(
            "https://www.youtube.com/channel/UC1",
            "p1",
            0,
            Arg.Any<CancellationToken>());
        Assert.True(row.IsSubscribed);
        Assert.False(row.CanSubscribe);
        Assert.Equal("Subscribed. New uploads will be downloaded automatically.", row.StatusMessage);
    }

    [Fact]
    public async Task SubscribeAsync_Failure_ShowsMessageAndStaysSubscribable()
    {
        _monitor.AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChannelSubscription>>(_ => throw new InvalidOperationException("You are already subscribed to One."));
        var row = CreateRow();

        await row.SubscribeAsync();

        Assert.False(row.IsSubscribed);
        Assert.True(row.CanSubscribe);
        Assert.Equal("You are already subscribed to One.", row.StatusMessage);
    }

    [Fact]
    public void AlreadySubscribed_CannotSubscribeAgain()
    {
        var row = CreateRow(isSubscribed: true);

        Assert.False(row.CanSubscribe);
        Assert.Equal("Subscribed.", row.StatusMessage);
    }

    [Fact]
    public void NoProfile_CannotSubscribe() =>
        Assert.False(CreateRow(profiles: []).CanSubscribe);

    [Theory]
    [InlineData("@one", 1_500L, "@one · 1.5K subscribers")]
    [InlineData(null, 1_500L, "1.5K subscribers")]
    [InlineData("@one", null, "@one")]
    [InlineData(null, null, "")]
    public void Subtitle_JoinsAvailableParts(string? handle, long? subscribers, string expected)
    {
        var row = CreateRow(handle: handle, subscribers: subscribers);

        Assert.Equal(expected, row.Subtitle);
    }

    private ChannelResultViewModel CreateRow(
        bool isSubscribed = false,
        IReadOnlyList<DownloadProfile>? profiles = null,
        string? handle = null,
        long? subscribers = null) =>
        new(
            new ChannelSearchResult
            {
                ChannelId = "UC1",
                Name = "One",
                ChannelUrl = "https://www.youtube.com/channel/UC1",
                Handle = handle,
                SubscriberCount = subscribers,
            },
            profiles ?? [Profile],
            defaultProfile: null,
            isSubscribed,
            _monitor,
            Substitute.For<IThumbnailLoader>());
}
