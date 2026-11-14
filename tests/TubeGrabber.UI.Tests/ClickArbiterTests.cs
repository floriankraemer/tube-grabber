// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class ClickArbiterTests
{
    private readonly List<string> _events = new();
    private readonly FakeScheduler _scheduler = new();
    private readonly ClickArbiter _arbiter;

    public ClickArbiterTests() =>
        _arbiter = new ClickArbiter(
            () => TimeSpan.FromMilliseconds(500),
            _scheduler.Schedule,
            () => _events.Add("single"),
            () => _events.Add("double"));

    [Fact]
    public void Click_WaitsForTheDoubleClickInterval_BeforeActing()
    {
        _arbiter.Click();

        Assert.Empty(_events);
        Assert.Equal(TimeSpan.FromMilliseconds(500), _scheduler.LastDelay);

        _scheduler.FireLast();

        Assert.Equal(["single"], _events);
    }

    [Fact]
    public void DoubleClick_CancelsThePendingSingleClick()
    {
        _arbiter.Click();
        _arbiter.DoubleClick();
        _scheduler.FireLast();

        Assert.Equal(["double"], _events);
    }

    [Fact]
    public void DoubleClick_WithoutAPendingClick_StillRuns()
    {
        _arbiter.DoubleClick();

        Assert.Equal(["double"], _events);
    }

    [Fact]
    public void TwoSeparateClicks_EachCountAsSingle()
    {
        _arbiter.Click();
        _scheduler.FireLast();
        _arbiter.Click();
        _scheduler.FireLast();

        Assert.Equal(["single", "single"], _events);
    }

    [Fact]
    public void Cancel_DropsThePendingClick()
    {
        _arbiter.Click();
        _arbiter.Cancel();
        _scheduler.FireLast();

        Assert.Empty(_events);
    }

    private sealed class FakeScheduler
    {
        private Action? _action;
        private bool _cancelled;

        public TimeSpan LastDelay { get; private set; }

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            LastDelay = delay;
            _action = action;
            _cancelled = false;
            return new Handle(this);
        }

        public void FireLast()
        {
            if (!_cancelled)
                _action?.Invoke();
        }

        private sealed class Handle(FakeScheduler owner) : IDisposable
        {
            public void Dispose() => owner._cancelled = true;
        }
    }
}
