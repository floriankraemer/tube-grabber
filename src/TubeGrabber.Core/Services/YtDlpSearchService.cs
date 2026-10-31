// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpSearchService : IYtDlpSearchService
{
    public const int PageSize = 20;

    /// <summary>
    /// Flat search results do not say whether a video is a Short, so anything up to this length counts as one.
    /// YouTube allows Shorts up to 3 minutes, but a higher cut-off would hide too many regular videos.
    /// </summary>
    public const int MaxShortDurationSeconds = 60;

    private readonly YtDlpFlatPlaylistRunner _runner;
    private readonly YtDlpSearchResultParser _parser;
    private readonly YtDlpChannelSearchParser _channelParser;

    public YtDlpSearchService(
        IYtDlpProcessRunner processRunner,
        IAppConfigStore appConfigStore,
        IBinaryLocator binaryLocator,
        IJsRuntimeLocator jsRuntimeLocator,
        YtDlpSearchResultParser? parser = null,
        YtDlpChannelSearchParser? channelParser = null)
    {
        _runner = new YtDlpFlatPlaylistRunner(processRunner, appConfigStore, binaryLocator, jsRuntimeLocator);
        _parser = parser ?? new YtDlpSearchResultParser();
        _channelParser = channelParser ?? new YtDlpChannelSearchParser();
    }

    public async Task<SearchResultPage> SearchAsync(
        string query,
        SearchFilters filters,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuery = ValidateQuery(query, skip);
        var output = await RunPageAsync(
            YouTubeSearchFilterEncoder.BuildVideoSearchUrl(trimmedQuery, filters),
            skip,
            "YouTube search failed. Check that a JS runtime is configured in Settings.",
            cancellationToken);

        var results = _parser.Parse(output);
        return new SearchResultPage
        {
            Query = trimmedQuery,
            Results = filters.HideShorts ? results.Where(r => !IsShort(r)).ToList() : results,
            Skip = skip,
            NextSkip = skip + results.Count,
            HasMoreResults = results.Count >= PageSize,
        };
    }

    public async Task<ChannelSearchPage> SearchChannelsAsync(
        string query,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuery = ValidateQuery(query, skip);
        var output = await RunPageAsync(
            YouTubeSearchFilterEncoder.BuildChannelSearchUrl(trimmedQuery),
            skip,
            "YouTube channel search failed. Check that a JS runtime is configured in Settings.",
            cancellationToken);

        var results = _channelParser.Parse(output);
        return new ChannelSearchPage
        {
            Query = trimmedQuery,
            Results = results,
            NextSkip = skip + results.Count,
            HasMoreResults = results.Count >= PageSize,
        };
    }

    public static IReadOnlyList<string> BuildSearchArguments(
        AppConfiguration config,
        IJsRuntimeLocator jsRuntimeLocator,
        string query,
        SearchFilters? filters = null,
        int skip = 0) =>
        YtDlpFlatPlaylistRunner.BuildArguments(
            config,
            jsRuntimeLocator,
            YouTubeSearchFilterEncoder.BuildVideoSearchUrl(query, filters ?? SearchFilters.Default),
            skip + 1,
            skip + PageSize);

    private static bool IsShort(YouTubeSearchResult result) =>
        result.DurationSeconds is <= MaxShortDurationSeconds;

    private static string ValidateQuery(string query, int skip)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("Enter a search query.");

        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        return query.Trim();
    }

    private Task<string> RunPageAsync(
        string target,
        int skip,
        string fallbackErrorMessage,
        CancellationToken cancellationToken) =>
        _runner.RunAsync(target, skip + 1, skip + PageSize, fallbackErrorMessage, cancellationToken);
}
