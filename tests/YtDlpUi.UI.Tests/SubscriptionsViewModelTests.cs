// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Tests;

public sealed class SubscriptionsViewModelTests
{
    private static readonly DownloadProfile Default = new() { Id = "default", Name = "Default" };
    private static readonly DownloadProfile Audio = new() { Id = "audio", Name = "Audio" };

    private readonly ISubscriptionMonitor _monitor = Substitute.For<ISubscriptionMonitor>();
    private readonly SubscriptionsViewModel _viewModel;

    public SubscriptionsViewModelTests()
    {
        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Audio, Default]);
        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription>());
        _viewModel = new SubscriptionsViewModel(_monitor, profileStore, appConfig);
    }

    private static ChannelSubscription Sub(string id, string profileId = "default", bool enabled = true) =>
        new()
        {
            Id = id,
            ChannelId = id,
            ChannelName = $"Channel {id}",
            ChannelUrl = $"https://www.youtube.com/@{id}",
            ProfileId = profileId,
            Enabled = enabled,
        };

    [Fact]
    public async Task InitializeAsync_LoadsProfilesAndSubscriptions()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription> { Sub("a", "audio") });

        await _viewModel.InitializeAsync();

        Assert.Equal(["Audio", "Default"], _viewModel.Profiles.Select(p => p.Name));
        Assert.Equal(Default, _viewModel.SelectedProfile);
        Assert.True(_viewModel.HasItems);
        Assert.Equal(Audio, _viewModel.Items[0].SelectedProfile);
    }

    [Fact]
    public async Task SubscribeAsync_EmptyUrl_SetsErrorAndSkipsMonitor()
    {
        await _viewModel.InitializeAsync();
        _viewModel.ChannelUrl = "  ";

        await _viewModel.SubscribeAsync();

        Assert.Equal("Enter a channel URL.", _viewModel.ErrorMessage);
        await _monitor.DidNotReceiveWithAnyArgs().AddAsync(default!, default!, default);
    }

    [Fact]
    public async Task SubscribeAsync_WithoutProfile_SetsError()
    {
        _viewModel.ChannelUrl = "https://www.youtube.com/@a";

        await _viewModel.SubscribeAsync();

        Assert.Equal("Select a profile.", _viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SubscribeAsync_Success_ReportsStatusClearsUrlAndRefreshes()
    {
        await _viewModel.InitializeAsync();
        var added = Sub("a");
        _monitor.AddAsync("https://www.youtube.com/@a", "default", 5, Arg.Any<CancellationToken>()).Returns(added);
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription> { added });
        _viewModel.ChannelUrl = " https://www.youtube.com/@a ";
        _viewModel.BacklogCount = 5;

        await _viewModel.SubscribeAsync();

        Assert.Equal("Subscribed to Channel a.", _viewModel.StatusMessage);
        Assert.Null(_viewModel.ErrorMessage);
        Assert.Equal(string.Empty, _viewModel.ChannelUrl);
        Assert.False(_viewModel.IsBusy);
        Assert.Single(_viewModel.Items);
    }

    [Fact]
    public async Task SubscribeAsync_Failure_ShowsMessageAndKeepsUrl()
    {
        await _viewModel.InitializeAsync();
        _monitor.AddAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException<ChannelSubscription>(new InvalidOperationException("Already subscribed.")));
        _viewModel.ChannelUrl = "https://www.youtube.com/@a";

        await _viewModel.SubscribeAsync();

        Assert.Equal("Already subscribed.", _viewModel.ErrorMessage);
        Assert.Equal("https://www.youtube.com/@a", _viewModel.ChannelUrl);
        Assert.False(_viewModel.IsBusy);
        Assert.True(_viewModel.CanSubscribe);
    }

    [Fact]
    public async Task SubscribeAsync_SetsBusyWhileRunning()
    {
        await _viewModel.InitializeAsync();
        var gate = new TaskCompletionSource<ChannelSubscription>();
        _monitor.AddAsync(default!, default!, default, default).ReturnsForAnyArgs(gate.Task);
        _viewModel.ChannelUrl = "https://www.youtube.com/@a";

        var running = _viewModel.SubscribeAsync();

        Assert.True(_viewModel.IsBusy);
        Assert.False(_viewModel.CanSubscribe);
        gate.SetResult(Sub("a"));
        await running;
        Assert.False(_viewModel.IsBusy);
    }

    [Fact]
    public void BacklogCount_IsClampedToRange()
    {
        Assert.Equal(3, _viewModel.BacklogCount);
        _viewModel.BacklogCount = 99;
        Assert.Equal(30, _viewModel.BacklogCount);
        _viewModel.BacklogCount = -4;
        Assert.Equal(0, _viewModel.BacklogCount);
    }

    [Fact]
    public async Task Remove_CallsMonitorAndRefreshes()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription> { Sub("a") });
        await _viewModel.InitializeAsync();
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription>());

        await _viewModel.Items[0].RemoveAsync();

        await _monitor.Received(1).RemoveAsync("a", Arg.Any<CancellationToken>());
        Assert.Empty(_viewModel.Items);
        Assert.False(_viewModel.HasItems);
    }

    [Fact]
    public async Task Remove_Failure_ShowsError()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription> { Sub("a") });
        await _viewModel.InitializeAsync();
        _monitor.RemoveAsync("a", Arg.Any<CancellationToken>()).Throws(new IOException("disk full"));

        await _viewModel.Items[0].RemoveAsync();

        Assert.Equal("disk full", _viewModel.ErrorMessage);
        Assert.Single(_viewModel.Items);
    }

    [Fact]
    public async Task CheckNowAsync_TogglesIsChecking()
    {
        var gate = new TaskCompletionSource();
        _monitor.CheckNowAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);

        var running = _viewModel.CheckNowAsync();

        Assert.True(_viewModel.IsChecking);
        Assert.False(_viewModel.CanCheckNow);
        gate.SetResult();
        await running;
        Assert.False(_viewModel.IsChecking);
        Assert.Equal("Check finished.", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task CheckNowAsync_Failure_ShowsError()
    {
        _monitor.CheckNowAsync(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("offline"));

        await _viewModel.CheckNowAsync();

        Assert.Equal("offline", _viewModel.ErrorMessage);
        Assert.False(_viewModel.IsChecking);
    }

    [Fact]
    public async Task RefreshAsync_ListFailure_ShowsError()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Throws(new IOException("corrupt"));

        await _viewModel.RefreshAsync();

        Assert.Equal("corrupt", _viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SubscriptionsChanged_RefreshesListKeepingItemInstances()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<ChannelSubscription> { Sub("a") });
        await _viewModel.InitializeAsync();
        var first = _viewModel.Items[0];

        var refreshed = new TaskCompletionSource();
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            refreshed.TrySetResult();
            return new List<ChannelSubscription> { Sub("a", enabled: false), Sub("b") };
        });

        _monitor.SubscriptionsChanged += Raise.Event<EventHandler>(_monitor, EventArgs.Empty);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => _viewModel.Items.Count == 2);

        Assert.Same(first, _viewModel.Items[0]);
        Assert.False(first.Enabled);
        Assert.Equal("Channel b", _viewModel.Items[1].ChannelName);
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromEvent()
    {
        _viewModel.Dispose();
        _monitor.ClearReceivedCalls();

        _monitor.SubscriptionsChanged += Raise.Event<EventHandler>(_monitor, EventArgs.Empty);
        await Task.Delay(50);

        await _monitor.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(50);
    }
}
