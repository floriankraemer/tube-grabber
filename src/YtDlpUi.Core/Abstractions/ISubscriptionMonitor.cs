// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Abstractions;

public interface ISubscriptionMonitor : IAsyncDisposable
{
    bool IsChecking { get; }

    event EventHandler? SubscriptionsChanged;

    void Start();

    Task<IReadOnlyList<ChannelSubscription>> ListAsync(CancellationToken ct = default);

    Task<ChannelSubscription> AddAsync(
        string channelUrl,
        string profileId,
        int backlogCount,
        CancellationToken ct = default);

    Task RemoveAsync(string id, CancellationToken ct = default);

    Task SetEnabledAsync(string id, bool enabled, CancellationToken ct = default);

    Task SetProfileAsync(string id, string profileId, CancellationToken ct = default);

    Task CheckNowAsync(CancellationToken ct = default);
}
