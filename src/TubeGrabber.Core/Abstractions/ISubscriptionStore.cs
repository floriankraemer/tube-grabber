// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Abstractions;

public interface ISubscriptionStore
{
    Task<IReadOnlyList<ChannelSubscription>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<ChannelSubscription> subscriptions, CancellationToken ct = default);
}
