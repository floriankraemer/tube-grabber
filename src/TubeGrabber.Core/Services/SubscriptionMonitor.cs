// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.Concurrent;
using System.Diagnostics;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class SubscriptionMonitor : ISubscriptionMonitor
{
    public const int MaxDownloadAttempts = 3;

    // ponytail: ScanWindow=30 — more uploads than that between scans are missed; page further if it matters.
    private const int ScanWindow = 30;
    private const int MaxTrackedFailures = 100;
    private static readonly TimeSpan SweepDebounce = TimeSpan.FromMilliseconds(250);

    private readonly ISubscriptionStore _store;
    private readonly IChannelFeedService _feed;
    private readonly DownloadEnqueueCoordinator _enqueue;
    private readonly IDownloadQueueService _queue;
    private readonly IAppConfigStore _config;
    private readonly IDesktopNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, TrackedJob> _tracked = new();
    private readonly CancellationTokenSource _stop = new();
    private List<ChannelSubscription>? _subscriptions;
    private Task? _loop;
    private Task _sweepWorker = Task.CompletedTask;
    private volatile bool _isChecking;
    private int _sweepRequests;
    private int _disposed;

    public SubscriptionMonitor(
        ISubscriptionStore store,
        IChannelFeedService feed,
        DownloadEnqueueCoordinator enqueue,
        IDownloadQueueService queue,
        IAppConfigStore config,
        IDesktopNotifier notifier,
        TimeProvider? timeProvider = null)
    {
        _store = store;
        _feed = feed;
        _enqueue = enqueue;
        _queue = queue;
        _config = config;
        _notifier = notifier;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _queue.JobsChanged += OnJobsChanged;
    }

    public bool IsChecking => _isChecking;

    public event EventHandler? SubscriptionsChanged;

    public void Start()
    {
        ThrowIfDisposed();
        _loop ??= Task.Run(RunLoopAsync);
    }

    public async Task<IReadOnlyList<ChannelSubscription>> ListAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(ct);
        try
        {
            return (await LoadLockedAsync(ct)).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChannelSubscription> AddAsync(
        string channelUrl,
        string profileId,
        int backlogCount,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        backlogCount = Math.Clamp(backlogCount, 0, ScanWindow);
        var feed = await _feed.GetLatestAsync(channelUrl, ScanWindow, ct);

        ChannelSubscription subscription;
        await _gate.WaitAsync(ct);
        try
        {
            var subscriptions = await LoadLockedAsync(ct);
            if (subscriptions.Any(s => s.ChannelId == feed.ChannelId))
                throw new InvalidOperationException($"You are already subscribed to {feed.ChannelName}.");

            subscription = new ChannelSubscription
            {
                ChannelId = feed.ChannelId,
                ChannelName = feed.ChannelName,
                ChannelUrl = feed.VideosUrl,
                ProfileId = profileId,
                CreatedAt = _timeProvider.GetUtcNow(),
            };
            foreach (var entry in feed.Entries.Skip(backlogCount))
                subscription.MarkSeen(entry.VideoId);

            subscriptions.Add(subscription);
            await _store.SaveAsync(subscriptions, ct);
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
        await ScanAsync(c => ScanSubscriptionAsync(subscription.Id, c), ct);
        return subscription;
    }

    public Task RemoveAsync(string id, CancellationToken ct = default) =>
        MutateAsync(subscriptions => subscriptions.RemoveAll(s => s.Id == id) > 0, ct);

    public Task SetEnabledAsync(string id, bool enabled, CancellationToken ct = default) =>
        MutateAsync(subscriptions => Update(subscriptions, id, s => s.Enabled = enabled), ct);

    public Task SetProfileAsync(string id, string profileId, CancellationToken ct = default) =>
        MutateAsync(subscriptions => Update(subscriptions, id, s => s.ProfileId = profileId), ct);

    public Task CheckNowAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ScanAsync(ScanAllAsync, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _queue.JobsChanged -= OnJobsChanged;
        await _stop.CancelAsync();
        await WaitQuietlyAsync(_loop);
        await WaitQuietlyAsync(_sweepWorker);
        _gate.Dispose();
        _scanLock.Dispose();
        _stop.Dispose();
    }

    private static async Task WaitQuietlyAsync(Task? task)
    {
        if (task is null)
            return;

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

    private async Task RunLoopAsync()
    {
        var ct = _stop.Token;
        while (!ct.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(AppPaths.MinSubscriptionCheckIntervalMinutes);
            try
            {
                await CheckNowAsync(ct);
                var config = await _config.LoadAsync(ct);
                delay = TimeSpan.FromMinutes(Math.Clamp(
                    config.SubscriptionCheckIntervalMinutes,
                    AppPaths.MinSubscriptionCheckIntervalMinutes,
                    AppPaths.MaxSubscriptionCheckIntervalMinutes));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Subscription check failed: {ex}");
            }

            try
            {
                await Task.Delay(delay, _timeProvider, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<List<ChannelSubscription>> LoadLockedAsync(CancellationToken ct) =>
        _subscriptions ??= (await _store.LoadAsync(ct)).ToList();

    private static ChannelSubscription? Find(List<ChannelSubscription> subscriptions, string id) =>
        subscriptions.FirstOrDefault(s => s.Id == id);

    private static bool Update(List<ChannelSubscription> subscriptions, string id, Action<ChannelSubscription> change)
    {
        var subscription = Find(subscriptions, id);
        if (subscription is null)
            return false;

        change(subscription);
        return true;
    }

    private async Task MutateAsync(Func<List<ChannelSubscription>, bool> mutation, CancellationToken ct)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(ct);
        try
        {
            var subscriptions = await LoadLockedAsync(ct);
            if (!mutation(subscriptions))
                return;

            await _store.SaveAsync(subscriptions, ct);
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
    }

    private async Task ScanAsync(Func<CancellationToken, Task> scan, CancellationToken ct)
    {
        await _scanLock.WaitAsync(ct);
        try
        {
            _isChecking = true;
            RaiseChanged();
            await scan(ct);
        }
        finally
        {
            _isChecking = false;
            _scanLock.Release();
        }

        RaiseChanged();
        OnJobsChanged(this, EventArgs.Empty);
    }

    private async Task ScanAllAsync(CancellationToken ct)
    {
        List<string> ids;
        await _gate.WaitAsync(ct);
        try
        {
            ids = (await LoadLockedAsync(ct)).Where(s => s.Enabled).Select(s => s.Id).ToList();
        }
        finally
        {
            _gate.Release();
        }

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            await ScanSubscriptionAsync(id, ct);
        }
    }

    // The feed is fetched outside _gate, so the subscription is looked up again afterwards: it may have been removed or disabled meanwhile.
    private async Task ScanSubscriptionAsync(string id, CancellationToken ct)
    {
        string channelUrl;
        await _gate.WaitAsync(ct);
        try
        {
            var current = Find(await LoadLockedAsync(ct), id);
            if (current is not { Enabled: true })
                return;

            channelUrl = current.ChannelUrl;
        }
        finally
        {
            _gate.Release();
        }

        ChannelFeed? feed = null;
        string? error = null;
        try
        {
            feed = await _feed.GetLatestAsync(channelUrl, ScanWindow, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        await _gate.WaitAsync(ct);
        try
        {
            var subscriptions = await LoadLockedAsync(ct);
            var current = Find(subscriptions, id);
            if (current is not { Enabled: true })
                return;

            if (feed is not null)
                error = await EnqueueNewEntriesAsync(current, feed, ct);

            current.LastError = error;
            current.LastCheckedAt = _timeProvider.GetUtcNow();
            await _store.SaveAsync(subscriptions, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> EnqueueNewEntriesAsync(ChannelSubscription subscription, ChannelFeed feed, CancellationToken ct)
    {
        try
        {
            PruneFailedAttempts(subscription, feed);
            var skip = _tracked.Values
                .Where(t => t.SubscriptionId == subscription.Id)
                .Select(t => t.VideoId)
                .Concat(subscription.SeenVideoIds)
                .ToHashSet();

            foreach (var entry in feed.Entries.Where(e => !skip.Contains(e.VideoId)).Reverse())
            {
                var result = await _enqueue.TryEnqueueAsync(entry.WatchUrl, subscription.ProfileId, ct);
                if (!result.IsSuccess || result.Job is null)
                    return result.Error ?? "Could not enqueue download.";

                _tracked[result.Job.Id] = new TrackedJob(subscription.Id, entry.VideoId, subscription.ChannelName, entry.Title);
            }

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static void PruneFailedAttempts(ChannelSubscription subscription, ChannelFeed feed)
    {
        if (subscription.FailedAttempts.Count <= MaxTrackedFailures)
            return;

        var inFeed = feed.Entries.Select(e => e.VideoId).ToHashSet();
        foreach (var stale in subscription.FailedAttempts.Keys.Where(k => !inFeed.Contains(k)).ToList())
            subscription.FailedAttempts.Remove(stale);
    }

    private void OnJobsChanged(object? sender, EventArgs e)
    {
        // JobsChanged fires on every progress line; a single debounced worker serves any number of requests.
        if (_tracked.IsEmpty || Volatile.Read(ref _disposed) == 1)
            return;

        if (Interlocked.Increment(ref _sweepRequests) == 1)
            _sweepWorker = Task.Run(SweepWorkerAsync);
    }

    private async Task SweepWorkerAsync()
    {
        try
        {
            while (true)
            {
                var seen = Volatile.Read(ref _sweepRequests);
                await Task.Delay(SweepDebounce, _timeProvider, _stop.Token);
                if (HasFinishedTrackedJob())
                    await SweepAsync();

                if (Interlocked.CompareExchange(ref _sweepRequests, 0, seen) == seen)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _sweepRequests, 0);
            Trace.TraceWarning($"Subscription job sweep failed: {ex}");
        }
    }

    private bool HasFinishedTrackedJob()
    {
        var active = _queue.Jobs
            .Where(j => j.Status is DownloadStatus.Queued or DownloadStatus.Running)
            .Select(j => j.Id)
            .ToHashSet();
        return _tracked.Keys.Any(id => !active.Contains(id));
    }

    private async Task SweepAsync()
    {
        var notifications = new List<(string Title, string Body)>();
        await _gate.WaitAsync(_stop.Token);
        try
        {
            var jobs = _queue.Jobs.ToDictionary(j => j.Id);
            var subscriptions = await LoadLockedAsync(_stop.Token);
            var dirty = false;
            foreach (var (jobId, tracked) in _tracked.ToList())
            {
                jobs.TryGetValue(jobId, out var job);
                if (job?.Status is DownloadStatus.Queued or DownloadStatus.Running)
                    continue;

                if (!_tracked.TryRemove(jobId, out _))
                    continue;

                dirty = true;
                var subscription = Find(subscriptions, tracked.SubscriptionId);
                var body = $"{tracked.ChannelName}: {job?.Title ?? tracked.EntryTitle}";
                if (job?.Status == DownloadStatus.Failed)
                {
                    if (subscription is not null && GiveUpAfterFailure(subscription, tracked.VideoId))
                        notifications.Add(("Download failed", $"{body} (gave up after {MaxDownloadAttempts} attempts)"));

                    continue;
                }

                subscription?.MarkSeen(tracked.VideoId);
                if (job?.Status == DownloadStatus.Completed)
                    notifications.Add(("New video downloaded", body));
            }

            if (dirty)
                await _store.SaveAsync(subscriptions, _stop.Token);
        }
        finally
        {
            _gate.Release();
        }

        foreach (var (title, body) in notifications)
        {
            try
            {
                await _notifier.ShowAsync(title, body, _stop.Token);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Desktop notification failed: {ex}");
            }
        }

        RaiseChanged();
    }

    private static bool GiveUpAfterFailure(ChannelSubscription subscription, string videoId)
    {
        var attempts = subscription.FailedAttempts.GetValueOrDefault(videoId) + 1;
        if (attempts < MaxDownloadAttempts)
        {
            subscription.FailedAttempts[videoId] = attempts;
            return false;
        }

        subscription.MarkSeen(videoId);
        return true;
    }

    private void RaiseChanged() => SubscriptionsChanged?.Invoke(this, EventArgs.Empty);

    private sealed record TrackedJob(string SubscriptionId, string VideoId, string ChannelName, string EntryTitle);
}
