// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class SubscriptionStoreTests : IDisposable
{
    private readonly string _root;
    private readonly SubscriptionStore _store;

    public SubscriptionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        _store = new SubscriptionStore(_root);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSubscriptions()
    {
        var subscription = CreateSubscription();
        subscription.LastError = "boom";
        subscription.MarkSeen("v1");

        await _store.SaveAsync([subscription], TestContext.Current.CancellationToken);
        var loaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        var single = Assert.Single(loaded);
        Assert.Equal(subscription.Id, single.Id);
        Assert.Equal(subscription.ChannelId, single.ChannelId);
        Assert.Equal(subscription.ChannelName, single.ChannelName);
        Assert.Equal(subscription.ChannelUrl, single.ChannelUrl);
        Assert.Equal(subscription.ProfileId, single.ProfileId);
        Assert.Equal("boom", single.LastError);
        Assert.Equal(["v1"], single.SeenVideoIds);
    }

    [Fact]
    public async Task LoadAsync_FileWithoutFailedAttempts_DefaultsToEmpty()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, AppPaths.SubscriptionsFileName),
            """{"Subscriptions":[{"Id":"a","ChannelId":"UC1","ChannelName":"C","ChannelUrl":"u","ProfileId":"p","SeenVideoIds":["v1"]}]}""", TestContext.Current.CancellationToken);

        var single = Assert.Single(await _store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Empty(single.FailedAttempts);
        Assert.Equal(["v1"], single.SeenVideoIds);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsFailedAttempts()
    {
        var subscription = CreateSubscription();
        subscription.FailedAttempts["v1"] = 2;

        await _store.SaveAsync([subscription], TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(await _store.LoadAsync(TestContext.Current.CancellationToken)).FailedAttempts["v1"]);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmpty()
    {
        var loaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ThrowsInvalidOperationException()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, AppPaths.SubscriptionsFileName), "{ not json", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_LeavesNoTempFile()
    {
        await _store.SaveAsync([CreateSubscription()], TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_root, AppPaths.SubscriptionsFileName)));
        Assert.False(File.Exists(Path.Combine(_root, AppPaths.SubscriptionsFileName + ".tmp")));
    }

    private static ChannelSubscription CreateSubscription() => new()
    {
        ChannelId = "UC123",
        ChannelName = "Channel",
        ChannelUrl = "https://www.youtube.com/channel/UC123",
        ProfileId = AppPaths.DefaultProfileId,
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
