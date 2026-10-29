// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.Concurrent;
using System.Diagnostics;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class SubscriptionMonitor : ISubscriptionMonitor
{
    // ponytail: ScanWindow=30 — more uploads than that between scans are missed; page further if it matters.
    private const int ScanWindow = 30;

    private readonly ISubscriptionStore _store;
    private readonly IChannelFeedService _feed;
    private readonly DownloadEnqueueCoordinator _enqueue;
    private readonly IDownloadQueueService _queue;
    private readonly IAppConfigStore _config;
    private readonly IDesktopNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, TrackedJob> _tracked = new();
    private readonly CancellationTokenSource _stop = new();
    private List<ChannelSubscription>? _subscriptions;
    private Task? _loop;
    private volatile bool _isChecking;
    private int _sweepPending;

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

    public void Start() => _loop ??= Task.Run(RunLoopAsync);

    public async Task<IReadOnlyList<ChannelSubscription>> ListAsync(CancellationToken ct = default)
    {
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
            await ScanLockedAsync(subscription, ct);
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
        OnJobsChanged(this, EventArgs.Empty);
        return subscription;
    }

    public Task RemoveAsync(string id, CancellationToken ct = default) =>
        MutateAsync(subscriptions => subscriptions.RemoveAll(s => s.Id == id) > 0, ct);

    public Task SetEnabledAsync(string id, bool enabled, CancellationToken ct = default) =>
        MutateAsync(subscriptions => Update(subscriptions, id, s => s.Enabled = enabled), ct);

    public Task SetProfileAsync(string id, string profileId, CancellationToken ct = default) =>
        MutateAsync(subscriptions => Update(subscriptions, id, s => s.ProfileId = profileId), ct);

    public async Task CheckNowAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _isChecking = true;
            RaiseChanged();
            var subscriptions = await LoadLockedAsync(ct);
            foreach (var subscription in subscriptions.Where(s => s.Enabled).ToList())
            {
                ct.ThrowIfCancellationRequested();
                await ScanLockedAsync(subscription, ct);
            }
        }
        finally
        {
            _isChecking = false;
            _gate.Release();
        }

        RaiseChanged();
        OnJobsChanged(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        _queue.JobsChanged -= OnJobsChanged;
        await _stop.CancelAsync();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

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
                Trace.TraceWarning($"Subscription check failed: {ex.Message}");
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

    private static bool Update(List<ChannelSubscription> subscriptions, string id, Action<ChannelSubscription> change)
    {
        var subscription = subscriptions.FirstOrDefault(s => s.Id == id);
        if (subscription is null)
            return false;

        change(subscription);
        return true;
    }

    private async Task MutateAsync(Func<List<ChannelSubscription>, bool> mutation, CancellationToken ct)
    {
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

    private async Task ScanLockedAsync(ChannelSubscription subscription, CancellationToken ct)
    {
        try
        {
            var feed = await _feed.GetLatestAsync(subscription.ChannelUrl, ScanWindow, ct);
            var skip = _tracked.Values
                .Where(t => t.SubscriptionId == subscription.Id)
                .Select(t => t.VideoId)
                .Concat(subscription.SeenVideoIds)
                .ToHashSet();

            subscription.LastError = null;
            foreach (var entry in feed.Entries.Where(e => !skip.Contains(e.VideoId)).Reverse())
            {
                var result = await _enqueue.TryEnqueueAsync(entry.WatchUrl, subscription.ProfileId, ct);
                if (!result.IsSuccess || result.Job is null)
                {
                    subscription.LastError = result.Error ?? "Could not enqueue download.";
                    break;
                }

                _tracked[result.Job.Id] = new TrackedJob(subscription.Id, entry.VideoId, subscription.ChannelName, entry.Title);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            subscription.LastError = ex.Message;
        }

        subscription.LastCheckedAt = _timeProvider.GetUtcNow();
        await _store.SaveAsync(await LoadLockedAsync(ct), ct);
    }

    private void OnJobsChanged(object? sender, EventArgs e)
    {
        // JobsChanged fires on every progress line; coalesce into at most one pending sweep.
        if (_tracked.IsEmpty || Interlocked.Exchange(ref _sweepPending, 1) == 1)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                Interlocked.Exchange(ref _sweepPending, 0);
                if (HasFinishedTrackedJob())
                    await SweepAsync();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Subscription job sweep failed: {ex.Message}");
            }
        });
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

                subscriptions.FirstOrDefault(s => s.Id == tracked.SubscriptionId)?.MarkSeen(tracked.VideoId);
                dirty = true;
                var body = $"{tracked.ChannelName}: {job?.Title ?? tracked.EntryTitle}";
                if (job?.Status == DownloadStatus.Completed)
                    notifications.Add(("New video downloaded", body));
                else if (job?.Status == DownloadStatus.Failed)
                    notifications.Add(("Download failed", body));
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
                Trace.TraceWarning($"Desktop notification failed: {ex.Message}");
            }
        }

        RaiseChanged();
    }

    private void RaiseChanged() => SubscriptionsChanged?.Invoke(this, EventArgs.Empty);

    private sealed record TrackedJob(string SubscriptionId, string VideoId, string ChannelName, string EntryTitle);
}
