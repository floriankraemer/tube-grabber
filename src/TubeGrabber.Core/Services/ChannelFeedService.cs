// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class ChannelFeedService : IChannelFeedService
{
    private const string NotChannelUrlMessage = "Enter a YouTube channel URL, e.g. https://www.youtube.com/@channel.";
    private const string ReadFailureMessage =
        "Could not read the channel. Check the URL and that a JS runtime is configured in Settings.";

    private static readonly HashSet<string> ChannelHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
    };

    private static readonly HashSet<string> NamedChannelPrefixes = new(StringComparer.Ordinal)
    {
        "channel",
        "c",
        "user",
    };

    private static readonly HashSet<string> ExcludedLiveStatuses = new(StringComparer.Ordinal)
    {
        "is_upcoming",
        "is_live",
        "post_live",
    };

    private static readonly HashSet<string> ExcludedAvailabilities = new(StringComparer.Ordinal)
    {
        "subscriber_only",
        "premium_only",
        "needs_auth",
    };

    private readonly YtDlpFlatPlaylistRunner _runner;
    private readonly YouTubeUrlNormalizer _urlNormalizer;
    private readonly YtDlpChannelFeedParser _parser;

    public ChannelFeedService(
        YtDlpFlatPlaylistRunner runner,
        YouTubeUrlNormalizer urlNormalizer,
        YtDlpChannelFeedParser? parser = null)
    {
        _runner = runner;
        _urlNormalizer = urlNormalizer;
        _parser = parser ?? new YtDlpChannelFeedParser();
    }

    public async Task<ChannelFeed> GetLatestAsync(
        string channelUrl,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var videosUrl = ToVideosTabUrl(channelUrl);
        videosUrl = _urlNormalizer.Normalize(videosUrl).NormalizedUrl ?? videosUrl;

        var output = await _runner.RunAsync(videosUrl, 1, count, ReadFailureMessage, cancellationToken);
        var feed = _parser.Parse(output)
            ?? throw new InvalidOperationException("Could not read the channel feed from yt-dlp output.");

        return new ChannelFeed
        {
            ChannelId = feed.ChannelId,
            ChannelName = feed.ChannelName,
            Entries = feed.Entries.Where(IsDownloadable).ToList(),
        };
    }

    public static string ToVideosTabUrl(string channelUrl)
    {
        var trimmed = channelUrl?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new InvalidOperationException("URL cannot be empty.");

        if (trimmed.StartsWith('@'))
            trimmed = $"https://www.youtube.com/{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("URL is not valid.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Only http and https URLs are supported.");

        if (!ChannelHosts.Contains(uri.Host))
            throw new InvalidOperationException("Only youtube.com channel URLs are supported.");

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 1 && segments[0].StartsWith('@') && segments[0].Length > 1)
            return $"https://www.youtube.com/{segments[0]}/videos";

        if (segments.Length >= 2 && NamedChannelPrefixes.Contains(segments[0]))
            return $"https://www.youtube.com/{segments[0]}/{segments[1]}/videos";

        throw new InvalidOperationException(NotChannelUrlMessage);
    }

    private static bool IsDownloadable(YouTubeSearchResult entry) =>
        !(entry.LiveStatus is not null && ExcludedLiveStatuses.Contains(entry.LiveStatus))
        && !(entry.Availability is not null && ExcludedAvailabilities.Contains(entry.Availability));
}
