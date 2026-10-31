// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Media.Imaging;

namespace TubeGrabber.UI.Services;

public interface IThumbnailLoader
{
    Task<Bitmap?> LoadAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class ThumbnailLoader : IThumbnailLoader
{
    public const int DecodeWidthPixels = 240;

    private readonly HttpClient _httpClient;

    public ThumbnailLoader(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<Bitmap?> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return Bitmap.DecodeToWidth(stream, DecodeWidthPixels);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
