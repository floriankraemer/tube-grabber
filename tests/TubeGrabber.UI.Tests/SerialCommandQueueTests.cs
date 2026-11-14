// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class SerialCommandQueueTests
{
    [Fact]
    public async Task Enqueue_RunsCommandsInOrderOneAtATime()
    {
        var queue = new SerialCommandQueue();
        var log = new List<int>();
        var concurrent = 0;
        var overlapped = false;

        for (var i = 0; i < 20; i++)
        {
            var n = i;
            queue.Enqueue(() =>
            {
                if (Interlocked.Increment(ref concurrent) > 1)
                    overlapped = true;
                log.Add(n);
                Interlocked.Decrement(ref concurrent);
            });
        }

        var done = new TaskCompletionSource();
        queue.Enqueue(() => done.SetResult());
        await done.Task;

        Assert.Equal(Enumerable.Range(0, 20), log);
        Assert.False(overlapped);
    }

    [Fact]
    public async Task Enqueue_SameKeyWhilePending_OnlyLatestRuns()
    {
        var queue = new SerialCommandQueue();
        var gate = new ManualResetEventSlim();
        var log = new List<string>();
        var started = new ManualResetEventSlim();
        queue.Enqueue(() =>
        {
            started.Set();
            gate.Wait(TestContext.Current.CancellationToken);
        });
        started.Wait(TestContext.Current.CancellationToken);
        queue.Enqueue(() => log.Add("seek1"), "seek");
        queue.Enqueue(() => log.Add("pause"), "pause");
        queue.Enqueue(() => log.Add("seek2"), "seek");
        queue.Enqueue(() => log.Add("seek3"), "seek");

        gate.Set();
        var done = new TaskCompletionSource();
        queue.Enqueue(() => done.SetResult());
        await done.Task;

        Assert.Equal(["seek3", "pause"], log);
    }

    [Fact]
    public async Task Close_DropsPendingRunsLastAfterInFlightAndRejectsNewCommands()
    {
        var queue = new SerialCommandQueue();
        var gate = new ManualResetEventSlim();
        var log = new List<string>();
        var started = new ManualResetEventSlim();
        queue.Enqueue(() =>
        {
            started.Set();
            gate.Wait(TestContext.Current.CancellationToken);
            log.Add("inflight");
        });
        started.Wait(TestContext.Current.CancellationToken);
        queue.Enqueue(() => log.Add("pending"));

        var closed = queue.Close(() => log.Add("teardown"));
        Assert.False(queue.Enqueue(() => log.Add("late")));
        gate.Set();
        await closed;

        Assert.Equal(["inflight", "teardown"], log);
    }

    [Fact]
    public async Task FailingCommand_DoesNotStopLaterCommands()
    {
        var queue = new SerialCommandQueue();
        var ran = false;
        queue.Enqueue(() => throw new InvalidOperationException("libvlc"));

        await queue.Close(() => ran = true);

        Assert.True(ran);
    }
}
