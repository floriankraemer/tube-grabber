// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

public sealed class YouTubeSearchResult
{
    public required string VideoId { get; init; }
    public required string Title { get; init; }
    public string? Channel { get; init; }
    public required string WatchUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public int? DurationSeconds { get; init; }
    public string? LiveStatus { get; init; }
    public string? Availability { get; init; }
}
