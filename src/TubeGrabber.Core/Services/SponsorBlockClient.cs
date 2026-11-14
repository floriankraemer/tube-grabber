// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

// Uses the k-anonymity lookup of the public SponsorBlock API: only the first 4 hex chars of SHA-256(videoId) leave
// the machine, and the response is filtered to the exact video id locally.
public sealed class SponsorBlockClient : ISponsorBlockClient
{
    private const string BaseUrl = "https://sponsor.ajay.app";
    private const string CategoriesJson = "[\"sponsor\"]";
    private const string ActionTypesJson = "[\"skip\"]";
    private const string UserAgent = "TubeGrabber";
    private const int HashPrefixLength = 4;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex VideoIdPattern = new(@"^[A-Za-z0-9_-]{11}\z", RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public SponsorBlockClient(HttpClient httpClient)
        : this(httpClient, DefaultTimeout)
    {
    }

    internal SponsorBlockClient(HttpClient httpClient, TimeSpan timeout)
    {
        _httpClient = httpClient;
        _timeout = timeout;
    }

    public async Task<IReadOnlyList<SponsorSegment>> GetSkipSegmentsAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        ValidateVideoId(videoId);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(videoId));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, null));

            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var entries = await JsonSerializer.DeserializeAsync<List<VideoSegmentsDto>>(stream, cancellationToken: timeoutCts.Token);
            return ToSegments(entries, videoId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Trace.TraceWarning("SponsorBlock lookup timed out; playing without skip segments.");
            return [];
        }
        catch (HttpRequestException ex)
        {
            Trace.TraceWarning($"SponsorBlock lookup failed; playing without skip segments: {ex}");
            return [];
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning($"SponsorBlock returned an invalid response; playing without skip segments: {ex}");
            return [];
        }
    }

    private static void ValidateVideoId(string videoId)
    {
        if (!VideoIdPattern.IsMatch(videoId))
            throw new ArgumentException("Expected an 11-character YouTube video id.", nameof(videoId));
    }

    private static Uri BuildUri(string videoId)
    {
        var prefix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(videoId)))[..HashPrefixLength];
        var query = $"categories={Uri.EscapeDataString(CategoriesJson)}&actionTypes={Uri.EscapeDataString(ActionTypesJson)}";
        return new Uri($"{BaseUrl}/api/skipSegments/{prefix}?{query}");
    }

    private static IReadOnlyList<SponsorSegment> ToSegments(List<VideoSegmentsDto>? entries, string videoId)
    {
        var entry = entries?.FirstOrDefault(e => string.Equals(e.VideoId, videoId, StringComparison.Ordinal));
        if (entry?.Segments is null)
            return [];

        return entry.Segments
            .Where(s => s.Uuid is not null && s.Category is not null && s.Segment is { Length: 2 } && s.Segment[1] > s.Segment[0])
            .Select(s => new SponsorSegment(s.Uuid!, s.Category!, s.Segment![0], s.Segment[1]))
            .OrderBy(s => s.StartSeconds)
            .ToList();
    }

    private sealed record VideoSegmentsDto(
        [property: JsonPropertyName("videoID")] string? VideoId,
        [property: JsonPropertyName("segments")] List<SegmentDto>? Segments);

    private sealed record SegmentDto(
        [property: JsonPropertyName("segment")] double[]? Segment,
        [property: JsonPropertyName("UUID")] string? Uuid,
        [property: JsonPropertyName("category")] string? Category);
}
