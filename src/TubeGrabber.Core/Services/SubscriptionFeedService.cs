// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class SubscriptionFeedService : ISubscriptionFeedService
{
    public const int MaxConcurrentChannels = 4;

    private readonly ISubscriptionStore _store;
    private readonly IChannelFeedService _feed;

    public SubscriptionFeedService(ISubscriptionStore store, IChannelFeedService feed)
    {
        _store = store;
        _feed = feed;
    }

    public async Task<IReadOnlyList<YouTubeSearchResult>> GetHomeFeedAsync(int perChannel, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(perChannel, 1);

        var subscriptions = (await _store.LoadAsync(ct)).Where(s => s.Enabled).ToList();
        var feeds = new IReadOnlyList<YouTubeSearchResult>?[subscriptions.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, subscriptions.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentChannels, CancellationToken = ct },
            async (index, token) => feeds[index] = await FetchChannelAsync(subscriptions[index], perChannel, token));

        return Interleave(feeds.OfType<IReadOnlyList<YouTubeSearchResult>>().ToList());
    }

    private async Task<IReadOnlyList<YouTubeSearchResult>?> FetchChannelAsync(
        ChannelSubscription subscription,
        int perChannel,
        CancellationToken ct)
    {
        try
        {
            var feed = await _feed.GetLatestAsync(subscription.ChannelUrl, perChannel, ct);
            return feed.Entries;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Home feed skipped channel {subscription.ChannelName}: {ex}");
            return null;
        }
    }

    // ponytail: flat-playlist entries lack reliable upload dates, so the feed interleaves channels round-robin
    // instead of sorting by date. Switch to a date sort once the feed carries timestamps.
    private static List<YouTubeSearchResult> Interleave(IReadOnlyList<IReadOnlyList<YouTubeSearchResult>> channels)
    {
        var seenVideoIds = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<YouTubeSearchResult>();
        var depth = channels.Count == 0 ? 0 : channels.Max(entries => entries.Count);

        for (var position = 0; position < depth; position++)
        {
            foreach (var entries in channels)
            {
                if (position < entries.Count && seenVideoIds.Add(entries[position].VideoId))
                    merged.Add(entries[position]);
            }
        }

        return merged;
    }
}
