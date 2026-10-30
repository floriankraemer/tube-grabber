// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Abstractions;

public interface IYtDlpSearchService
{
    Task<SearchResultPage> SearchAsync(
        string query,
        SearchFilters filters,
        int skip = 0,
        CancellationToken cancellationToken = default);

    Task<ChannelSearchPage> SearchChannelsAsync(
        string query,
        int skip = 0,
        CancellationToken cancellationToken = default);
}
