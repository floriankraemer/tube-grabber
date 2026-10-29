// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.IO.Pipes;
using YtDlpUi.UI.Services;

namespace YtDlpUi.UI.Tests;

public sealed class SingleInstanceGuardTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("single-instance-tests-").FullName;
    private readonly string _pipeName = $"yt-dlp-ui-test-{Guid.NewGuid():N}";

    private SingleInstanceGuard NewGuard() => new(Path.Combine(_root, "sub", "instance.lock"), _pipeName);

    [Fact]
    public void TryAcquire_SecondInstanceFailsWhileFirstHoldsLock()
    {
        using var first = NewGuard();
        using var second = NewGuard();

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void TryAcquire_SucceedsAgainAfterFirstIsDisposed()
    {
        var first = NewGuard();
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var second = NewGuard();
        Assert.True(second.TryAcquire());
    }

    [Fact]
    public async Task NotifyExistingInstance_DeliversShowToListener()
    {
        using var first = NewGuard();
        var shown = new TaskCompletionSource();
        first.StartListening(() => shown.TrySetResult());

        using var second = NewGuard();
        Assert.True(await Task.Run(() => NotifyWithRetry(second)));

        await shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Listener_IgnoresUnknownMessagesAndKeepsServing()
    {
        using var first = NewGuard();
        var shown = new TaskCompletionSource();
        first.StartListening(() => shown.TrySetResult());

        await SendRawAsync("bogus");
        await SendRawAsync(SingleInstanceGuard.ShowMessage);

        await shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void NotifyExistingInstance_ReturnsFalseWithoutListener()
    {
        using var guard = NewGuard();

        Assert.False(guard.NotifyExistingInstance());
    }

    private static bool NotifyWithRetry(SingleInstanceGuard guard)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (guard.NotifyExistingInstance())
                return true;
            Thread.Sleep(100);
        }

        return false;
    }

    private async Task SendRawAsync(string message)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
        await client.ConnectAsync(5000);
        await using var writer = new StreamWriter(client) { AutoFlush = true };
        await writer.WriteLineAsync(message);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
