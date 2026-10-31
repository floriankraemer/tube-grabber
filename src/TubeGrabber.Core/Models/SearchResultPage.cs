// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class SearchResultPage
{
    public required string Query { get; init; }
    public required IReadOnlyList<YouTubeSearchResult> Results { get; init; }
    public int Skip { get; init; }

    /// <summary>Offset of the next page. Differs from <c>Skip + Results.Count</c> when results were filtered out.</summary>
    public int NextSkip { get; init; }

    public bool HasMoreResults { get; init; }
}
