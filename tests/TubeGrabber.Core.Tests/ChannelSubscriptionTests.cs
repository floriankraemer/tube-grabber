// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Tests;

public sealed class ChannelSubscriptionTests
{
    [Fact]
    public void MarkSeen_IgnoresDuplicates()
    {
        var subscription = CreateSubscription();

        subscription.MarkSeen("v1");
        subscription.MarkSeen("v1");

        Assert.Equal(["v1"], subscription.SeenVideoIds);
    }

    [Fact]
    public void MarkSeen_ClearsFailedAttempts()
    {
        var subscription = CreateSubscription();
        subscription.FailedAttempts["v1"] = 2;

        subscription.MarkSeen("v1");

        Assert.Empty(subscription.FailedAttempts);
    }

    [Fact]
    public void MarkSeen_TrimsOldestBeyondCap()
    {
        var subscription = CreateSubscription();
        var cap = AppPaths.MaxSeenVideoIdsPerSubscription;

        for (var i = 0; i < cap + 2; i++)
            subscription.MarkSeen($"v{i}");

        Assert.Equal(cap, subscription.SeenVideoIds.Count);
        Assert.Equal("v2", subscription.SeenVideoIds[0]);
        Assert.Equal($"v{cap + 1}", subscription.SeenVideoIds[^1]);
    }

    private static ChannelSubscription CreateSubscription() => new()
    {
        ChannelId = "UC123",
        ChannelName = "Channel",
        ChannelUrl = "https://www.youtube.com/channel/UC123",
        ProfileId = AppPaths.DefaultProfileId,
    };
}
