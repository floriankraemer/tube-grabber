// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Globalization;
using System.Text.Json;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpVideoDetailsParser
{
    /// <exception cref="InvalidOperationException">The output is not a yt-dlp video JSON document.</exception>
    public VideoDetails Parse(string output)
    {
        var trimmed = output?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith('{'))
            throw new InvalidOperationException("yt-dlp returned no video details.");

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return ParseRoot(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("yt-dlp returned video details that could not be read.", ex);
        }
    }

    private static VideoDetails ParseRoot(JsonElement root)
    {
        var id = ReadString(root, "id");
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("yt-dlp video details have no video id.");

        var streams = YtDlpStreamPicker.Pick(ReadFormats(root));
        return new VideoDetails
        {
            Id = id,
            Title = ReadString(root, "title") ?? id,
            Channel = ReadString(root, "channel") ?? ReadString(root, "uploader"),
            ChannelUrl = ReadString(root, "channel_url") ?? ReadString(root, "uploader_url"),
            Description = ReadString(root, "description"),
            ViewCount = ReadDouble(root, "view_count") is { } views ? (long)views : null,
            UploadDate = ReadUploadDate(root),
            DurationSeconds = ReadDouble(root, "duration") is { } d ? (int)Math.Round(d) : null,
            ThumbnailUrl = ReadString(root, "thumbnail"),
            VideoStreamUrl = streams.VideoUrl,
            AudioStreamUrl = streams.AudioUrl,
            StreamsAvailableAt = streams.AvailableAt,
        };
    }

    private static List<StreamFormat> ReadFormats(JsonElement root)
    {
        var formats = new List<StreamFormat>();
        if (!root.TryGetProperty("formats", out var array) || array.ValueKind != JsonValueKind.Array)
            return formats;

        foreach (var f in array.EnumerateArray().Where(f => f.ValueKind == JsonValueKind.Object))
        {
            formats.Add(new StreamFormat(
                ReadString(f, "url"),
                ReadString(f, "vcodec"),
                ReadString(f, "acodec"),
                ReadString(f, "ext"),
                ReadString(f, "protocol"),
                ReadString(f, "format_note"),
                ReadDouble(f, "height") is { } h ? (int)h : null,
                ReadDouble(f, "tbr"),
                ReadDouble(f, "abr"),
                ReadDouble(f, "available_at") is { } at ? DateTimeOffset.FromUnixTimeSeconds((long)Math.Ceiling(at)) : null));
        }

        return formats;
    }

    private static DateOnly? ReadUploadDate(JsonElement root) =>
        DateOnly.TryParseExact(ReadString(root, "upload_date"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d)
            ? d
            : null;
}
