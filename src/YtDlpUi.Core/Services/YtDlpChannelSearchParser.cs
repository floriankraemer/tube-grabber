// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class YtDlpChannelSearchParser
{
    public IReadOnlyList<ChannelSearchResult> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var trimmed = output.Trim();
        if (!trimmed.StartsWith('{'))
            return [];

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (!document.RootElement.TryGetProperty("entries", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
                return [];

            return entries.EnumerateArray()
                .Select(ParseEntry)
                .OfType<ChannelSearchResult>()
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ChannelSearchResult? ParseEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        var channelId = ReadString(entry, "channel_id") ?? ReadString(entry, "id");
        if (string.IsNullOrWhiteSpace(channelId))
            return null;

        var handle = ReadString(entry, "uploader_id");
        return new ChannelSearchResult
        {
            ChannelId = channelId,
            Name = ReadString(entry, "title") ?? ReadString(entry, "channel") ?? channelId,
            ChannelUrl = ReadString(entry, "channel_url")
                ?? ReadString(entry, "url")
                ?? $"https://www.youtube.com/channel/{channelId}",
            Handle = handle is not null && handle.StartsWith('@') ? handle : null,
            ThumbnailUrl = ReadThumbnailUrl(entry),
            SubscriberCount = entry.TryGetProperty("channel_follower_count", out var count)
                && count.ValueKind == JsonValueKind.Number
                && count.TryGetInt64(out var followers)
                    ? followers
                    : null,
        };
    }

    // Avatar URLs come back protocol-relative ("//yt3.ggpht.com/..."); the widest one wins.
    private static string? ReadThumbnailUrl(JsonElement entry)
    {
        if (!entry.TryGetProperty("thumbnails", out var thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
            return null;

        string? best = null;
        var bestWidth = -1;
        foreach (var item in thumbnails.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || ReadString(item, "url") is not { Length: > 0 } url)
                continue;

            if (url.StartsWith("//", StringComparison.Ordinal))
                url = "https:" + url;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;

            var width = item.TryGetProperty("width", out var w) && w.TryGetInt32(out var value) ? value : 0;
            if (width >= bestWidth)
            {
                bestWidth = width;
                best = url;
            }
        }

        return best;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
