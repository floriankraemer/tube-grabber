// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class ChannelSearchResult
{
    public required string ChannelId { get; init; }
    public required string Name { get; init; }
    public required string ChannelUrl { get; init; }
    public string? Handle { get; init; }
    public string? ThumbnailUrl { get; init; }
    public long? SubscriberCount { get; init; }
}
