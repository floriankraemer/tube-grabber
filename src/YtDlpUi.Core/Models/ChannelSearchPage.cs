// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

public sealed class ChannelSearchPage
{
    public required string Query { get; init; }
    public required IReadOnlyList<ChannelSearchResult> Results { get; init; }
    public int NextSkip { get; init; }
    public bool HasMoreResults { get; init; }
}
