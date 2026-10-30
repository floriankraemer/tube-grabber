// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Net;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

/// <summary>
/// Builds YouTube search-results URLs with the <c>sp</c> filter parameter, a base64 protobuf message:
/// <c>filters { 1: uploadDate, 2: type, 3: duration }</c>.
/// </summary>
public static class YouTubeSearchFilterEncoder
{
    private const byte FiltersField = 0x12;
    private const byte UploadDateField = 0x08;
    private const byte TypeField = 0x10;
    private const byte DurationField = 0x18;
    private const byte VideoType = 1;
    private const byte ChannelType = 2;

    public static string BuildVideoSearchUrl(string query, SearchFilters filters) =>
        BuildUrl(query, Encode((byte)filters.UploadDate, VideoType, (byte)filters.Duration));

    public static string BuildChannelSearchUrl(string query) =>
        BuildUrl(query, Encode(0, ChannelType, 0));

    private static string BuildUrl(string query, string sp) =>
        $"https://www.youtube.com/results?search_query={WebUtility.UrlEncode(query)}&sp={Uri.EscapeDataString(sp)}";

    private static string Encode(byte uploadDate, byte type, byte duration)
    {
        var message = new List<byte>();
        if (uploadDate != 0)
            message.AddRange([UploadDateField, uploadDate]);
        message.AddRange([TypeField, type]);
        if (duration != 0)
            message.AddRange([DurationField, duration]);

        return Convert.ToBase64String([FiltersField, (byte)message.Count, .. message]);
    }
}
