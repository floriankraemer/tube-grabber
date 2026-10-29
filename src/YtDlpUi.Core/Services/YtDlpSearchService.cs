// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class YtDlpSearchService : IYtDlpSearchService
{
    public const int PageSize = 20;

    private readonly YtDlpFlatPlaylistRunner _runner;
    private readonly YtDlpSearchResultParser _parser;

    public YtDlpSearchService(
        IYtDlpProcessRunner processRunner,
        IAppConfigStore appConfigStore,
        IBinaryLocator binaryLocator,
        IJsRuntimeLocator jsRuntimeLocator,
        YtDlpSearchResultParser? parser = null)
    {
        _runner = new YtDlpFlatPlaylistRunner(processRunner, appConfigStore, binaryLocator, jsRuntimeLocator);
        _parser = parser ?? new YtDlpSearchResultParser();
    }

    public Task<SearchResultPage> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        SearchAsync(query, skip: 0, cancellationToken);

    public async Task<SearchResultPage> SearchAsync(
        string query,
        int skip,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("Enter a search query.");

        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip));

        var trimmedQuery = query.Trim();
        var end = skip + PageSize;
        var output = await _runner.RunAsync(
            $"ytsearch{end}:{trimmedQuery}",
            skip + 1,
            end,
            "YouTube search failed. Check that a JS runtime is configured in Settings.",
            cancellationToken);

        var results = _parser.Parse(output);
        return new SearchResultPage
        {
            Query = trimmedQuery,
            Results = results,
            Skip = skip,
            HasMoreResults = results.Count >= PageSize,
        };
    }

    public static IReadOnlyList<string> BuildSearchArguments(
        Models.AppConfiguration config,
        IJsRuntimeLocator jsRuntimeLocator,
        string query,
        int skip = 0)
    {
        var end = skip + PageSize;
        return YtDlpFlatPlaylistRunner.BuildArguments(
            config,
            jsRuntimeLocator,
            $"ytsearch{end}:{query}",
            skip + 1,
            end);
    }
}
