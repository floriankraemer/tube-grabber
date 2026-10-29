// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

public sealed class ChannelFeed
{
    public required string ChannelId { get; init; }
    public required string ChannelName { get; init; }
    public required IReadOnlyList<YouTubeSearchResult> Entries { get; init; }
}
