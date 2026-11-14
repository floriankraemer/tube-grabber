// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class SponsorBlockClientTests
{
    private const string VideoId = "dQw4w9WgXcQ";
    private const string OtherVideoId = "aaaaaaaaaaa";

    private static readonly string[] EncodedFilters =
    [
        "categories=%5B%22sponsor%22%5D",
        "actionTypes=%5B%22skip%22%5D",
    ];

    [Fact]
    public async Task GetSkipSegments_RequestsOnlyTheHashPrefixWithEncodedFilters()
    {
        var prefix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(VideoId)))[..4];
        string? requestedUri = null;
        string? userAgent = null;
        var client = CreateClient((request, _) =>
        {
            requestedUri = request.RequestUri!.AbsoluteUri;
            userAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(Json("[]"));
        });

        await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"https://sponsor.ajay.app/api/skipSegments/{prefix}?categories=%5B%22sponsor%22%5D&actionTypes=%5B%22skip%22%5D",
            requestedUri);
        Assert.Equal("TubeGrabber", userAgent);
    }

    [Fact]
    public async Task GetSkipSegments_ReturnsOnlySegmentsOfTheRequestedVideo()
    {
        var body = $$"""
            [
              {"videoID":"{{OtherVideoId}}","segments":[{"segment":[1,2],"UUID":"other-1","category":"sponsor","actionType":"skip"}]},
              {"videoID":"{{VideoId}}","segments":[{"segment":[30.5,45.0],"UUID":"u1","category":"sponsor","actionType":"skip"}]}
            ]
            """;
        var client = CreateClient((_, _) => Task.FromResult(Json(body)));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        var segment = Assert.Single(segments);
        Assert.Equal("u1", segment.Uuid);
        Assert.Equal("sponsor", segment.Category);
        Assert.Equal(30.5, segment.StartSeconds);
        Assert.Equal(45.0, segment.EndSeconds);
    }

    [Fact]
    public async Task GetSkipSegments_DropsEmptyOrReversedSegmentsAndSortsByStart()
    {
        var body = $$"""
            [{"videoID":"{{VideoId}}","segments":[
              {"segment":[50,60],"UUID":"late","category":"sponsor"},
              {"segment":[10,10],"UUID":"empty","category":"sponsor"},
              {"segment":[5,8],"UUID":"early","category":"sponsor"},
              {"segment":[40,30],"UUID":"reversed","category":"sponsor"}
            ]}]
            """;
        var client = CreateClient((_, _) => Task.FromResult(Json(body)));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Equal(["early", "late"], segments.Select(s => s.Uuid));
    }

    [Fact]
    public async Task GetSkipSegments_NotFound_ReturnsEmpty()
    {
        var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task GetSkipSegments_ServerError_ReturnsEmpty()
    {
        var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task GetSkipSegments_NetworkError_ReturnsEmpty()
    {
        var client = CreateClient((_, _) => throw new HttpRequestException("network down"));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task GetSkipSegments_InvalidJson_ReturnsEmpty()
    {
        var client = CreateClient((_, _) => Task.FromResult(Json("{not json")));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task GetSkipSegments_RequestExceedsTimeout_ReturnsEmpty()
    {
        var client = CreateClient(
            async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return Json("[]");
            },
            timeout: TimeSpan.FromMilliseconds(50));

        var segments = await client.GetSkipSegmentsAsync(VideoId, TestContext.Current.CancellationToken);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task GetSkipSegments_CallerCancelsInFlight_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = CreateClient(
            async (_, ct) =>
            {
                requestStarted.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return Json("[]");
            });

        var lookup = client.GetSkipSegmentsAsync(VideoId, cancellation.Token);
        await requestStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("dQw4w9WgXcQQ")]
    [InlineData("dQw4w9Wg/cQ")]
    [InlineData("dQw4w9WgXc\n")]
    public async Task GetSkipSegments_InvalidVideoId_ThrowsArgumentException(string videoId)
    {
        var client = CreateClient((_, _) => throw new InvalidOperationException("must not send a request"));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetSkipSegmentsAsync(videoId, TestContext.Current.CancellationToken));
    }

    private static SponsorBlockClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(new FakeHandler(respond));
        return new SponsorBlockClient(httpClient, timeout ?? TimeSpan.FromSeconds(5));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
