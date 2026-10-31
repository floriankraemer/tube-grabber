// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YouTubeUrlNormalizer
{
    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtu.be",
        "www.youtu.be",
    };

    public UrlNormalizationResult Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return UrlNormalizationResult.Failure("URL cannot be empty.");

        var trimmed = input.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return UrlNormalizationResult.Failure("URL is not valid.");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return UrlNormalizationResult.Failure("Only http and https URLs are supported.");

        if (!IsYouTubeHost(uri.Host))
            return UrlNormalizationResult.Success(trimmed);

        return UrlNormalizationResult.Success(NormalizeYouTubeUri(uri));
    }

    private static string NormalizeYouTubeUri(Uri uri)
    {
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.AbsolutePath.Trim('/');
            return string.IsNullOrWhiteSpace(path)
                ? new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri.ToString()
                : $"{uri.Scheme}://{uri.Host}/{path}";
        }

        var videoId = ExtractVideoId(uri);
        if (videoId is not null)
        {
            var builder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, "/watch")
            {
                Query = $"v={videoId}",
            };
            return builder.Uri.ToString();
        }

        var stripped = new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty };
        return stripped.Uri.ToString();
    }

    private static string? ExtractVideoId(Uri uri)
    {
        if (!string.IsNullOrEmpty(uri.Query))
        {
            foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2
                    && string.Equals(keyValue[0], "v", StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(keyValue[1]);
            }
        }

        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri.AbsolutePath.Trim('/');
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }

        return null;
    }

    private static bool IsYouTubeHost(string host)
    {
        if (YouTubeHosts.Contains(host))
            return true;

        return host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);
    }
}
