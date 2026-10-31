// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SubscriptionItemViewModelTests
{
    private static readonly DownloadProfile Default = new() { Id = "default", Name = "Default" };
    private static readonly DownloadProfile Audio = new() { Id = "audio", Name = "Audio" };

    private readonly ISubscriptionMonitor _monitor = Substitute.For<ISubscriptionMonitor>();
    private readonly List<string?> _errors = [];

    [Fact]
    public void Initial_IsUppercasedFirstLetterOfChannelName() =>
        Assert.Equal("C", Create().Initial);

    private SubscriptionItemViewModel Create(ChannelSubscription? subscription = null) =>
        new(
            subscription ?? Sub(),
            [Default, Audio],
            _monitor,
            _ => Task.CompletedTask,
            _errors.Add);

    private static ChannelSubscription Sub() =>
        new()
        {
            Id = "a",
            ChannelId = "a",
            ChannelName = "Channel A",
            ChannelUrl = "https://www.youtube.com/@a",
            ProfileId = "default",
        };

    [Fact]
    public void Constructor_DoesNotCallMonitor()
    {
        var item = Create();

        Assert.True(item.Enabled);
        Assert.Equal(Default, item.SelectedProfile);
        Assert.Empty(_monitor.ReceivedCalls());
    }

    [Fact]
    public void LastCheckedDisplay_NeverChecked()
    {
        Assert.Equal("Never checked", Create().LastCheckedDisplay);
    }

    [Fact]
    public void LastCheckedDisplay_ShowsLocalTime()
    {
        var sub = Sub();
        sub.LastCheckedAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.StartsWith("Last checked ", Create(sub).LastCheckedDisplay);
    }

    [Fact]
    public void LastError_ExposedWithFlag()
    {
        var sub = Sub();
        Assert.False(Create(sub).HasError);

        sub.LastError = "boom";
        var item = Create(sub);

        Assert.True(item.HasError);
        Assert.Equal("boom", item.LastError);
    }

    [Fact]
    public void Enabled_Change_CallsMonitor()
    {
        var item = Create();

        item.Enabled = false;

        _monitor.Received(1).SetEnabledAsync("a", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedProfile_Change_CallsMonitor()
    {
        var item = Create();

        item.SelectedProfile = Audio;

        _monitor.Received(1).SetProfileAsync("a", "audio", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedProfile_Null_DoesNotCallMonitor()
    {
        var item = Create();

        item.SelectedProfile = null;

        _monitor.DidNotReceiveWithAnyArgs().SetProfileAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Update_SyncsStateWithoutCallingMonitor()
    {
        var item = Create();
        var changed = Sub();
        changed.Enabled = false;
        changed.ProfileId = "audio";
        changed.LastError = "x";

        item.Update(changed);

        Assert.False(item.Enabled);
        Assert.Equal(Audio, item.SelectedProfile);
        Assert.True(item.HasError);
        Assert.Empty(_monitor.ReceivedCalls());
    }

    [Fact]
    public void Enabled_MonitorFailure_ReportsError()
    {
        _monitor.SetEnabledAsync("a", false, Arg.Any<CancellationToken>()).Throws(new IOException("nope"));
        var item = Create();

        item.Enabled = false;

        Assert.Contains("nope", _errors);
    }

    [Fact]
    public void Enabled_MonitorFailure_RevertsWithoutSecondCall()
    {
        _monitor.SetEnabledAsync("a", false, Arg.Any<CancellationToken>()).Throws(new IOException("nope"));
        var item = Create();

        item.Enabled = false;

        Assert.True(item.Enabled);
        _monitor.Received(1).SetEnabledAsync("a", Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedProfile_MonitorFailure_RevertsWithoutSecondCall()
    {
        _monitor.SetProfileAsync("a", "audio", Arg.Any<CancellationToken>()).Throws(new IOException("nope"));
        var item = Create();

        item.SelectedProfile = Audio;

        Assert.Equal(Default, item.SelectedProfile);
        Assert.Contains("nope", _errors);
        _monitor.Received(1).SetProfileAsync("a", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
