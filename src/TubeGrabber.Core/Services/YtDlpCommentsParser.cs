// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpCommentsParser
{
    private const string RootParent = "root";

    private static readonly long MinUnixSeconds = DateTimeOffset.MinValue.ToUnixTimeSeconds();
    private static readonly long MaxUnixSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    /// <summary>
    /// Parses yt-dlp's single JSON document and keeps top-level comments only.
    /// Comments disabled (<c>comments</c> missing or null) and malformed output both yield an empty list.
    /// </summary>
    public IReadOnlyList<VideoComment> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var trimmed = output.Trim();
        if (!trimmed.StartsWith('{'))
            return [];

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (!document.RootElement.TryGetProperty("comments", out var comments)
                || comments.ValueKind != JsonValueKind.Array)
                return [];

            return ParseComments(comments);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<VideoComment> ParseComments(JsonElement comments)
    {
        var results = new List<VideoComment>();
        foreach (var entry in comments.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || ReadString(entry, "parent") != RootParent)
                continue;

            var id = ReadString(entry, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            results.Add(new VideoComment
            {
                Id = id,
                Author = ReadString(entry, "author") ?? string.Empty,
                Text = ReadString(entry, "text") ?? string.Empty,
                LikeCount = ReadLong(entry, "like_count"),
                PostedAt = ReadUnixTimestamp(entry, "timestamp"),
                IsPinned = ReadBool(entry, "is_pinned"),
                AuthorIsUploader = ReadBool(entry, "author_is_uploader"),
            });
        }

        return results;
    }

    private static DateTimeOffset? ReadUnixTimestamp(JsonElement entry, string propertyName)
    {
        var seconds = ReadLong(entry, propertyName);
        return seconds is { } value && value >= MinUnixSeconds && value <= MaxUnixSeconds
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : null;
    }

    private static long? ReadLong(JsonElement entry, string propertyName) =>
        entry.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    private static bool ReadBool(JsonElement entry, string propertyName) =>
        entry.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement entry, string propertyName) =>
        entry.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
