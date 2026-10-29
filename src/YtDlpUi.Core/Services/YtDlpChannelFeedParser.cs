// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class YtDlpChannelFeedParser
{
    private const string TabTitleSuffix = " - Videos";

    private readonly YtDlpSearchResultParser _parser;

    public YtDlpChannelFeedParser(YtDlpSearchResultParser? parser = null)
    {
        _parser = parser ?? new YtDlpSearchResultParser();
    }

    public ChannelFeed? Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var trimmed = output.Trim();
        if (!trimmed.StartsWith('{'))
            return null;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;

            var channelId = ReadFirst(root, "channel_id", "uploader_id", "id");
            if (channelId is null)
                return null;

            var channelName = ReadChannelName(root) ?? channelId;
            var entries = _parser.Parse(BuildEntriesJson(root));

            return new ChannelFeed
            {
                ChannelId = channelId,
                ChannelName = channelName,
                Entries = entries,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadChannelName(JsonElement root)
    {
        var name = ReadFirst(root, "channel", "uploader", "title");
        if (name is null)
            return null;

        return name.EndsWith(TabTitleSuffix, StringComparison.Ordinal)
            ? name[..^TabTitleSuffix.Length]
            : name;
    }

    // yt-dlp wraps channel tabs (Videos, Shorts, Live) as nested playlists; their entries are flattened one level.
    private static string BuildEntriesJson(JsonElement root)
    {
        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return "{}";

        var rawEntries = new List<string>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (IsNestedPlaylist(entry, out var nested))
                rawEntries.AddRange(nested.EnumerateArray().Select(item => item.GetRawText()));
            else
                rawEntries.Add(entry.GetRawText());
        }

        return $"{{\"entries\":[{string.Join(',', rawEntries)}]}}";
    }

    private static bool IsNestedPlaylist(JsonElement entry, out JsonElement nested)
    {
        nested = default;
        return entry.ValueKind == JsonValueKind.Object
            && entry.TryGetProperty("_type", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "playlist"
            && entry.TryGetProperty("entries", out nested)
            && nested.ValueKind == JsonValueKind.Array;
    }

    private static string? ReadFirst(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
