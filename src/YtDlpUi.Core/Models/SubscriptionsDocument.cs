// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

public sealed class SubscriptionsDocument
{
    public int SchemaVersion { get; set; } = Constants.SchemaVersions.Subscriptions;
    public List<ChannelSubscription> Subscriptions { get; set; } = [];
}
