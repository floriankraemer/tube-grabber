// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class DownloadEnqueueCoordinator
{
    private readonly IDownloadQueueService _queue;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly YouTubeUrlNormalizer _urlNormalizer;
    private readonly IBinaryLocator _binaryLocator;
    private readonly DownloadFolderService _downloadFolderService;

    public DownloadEnqueueCoordinator(
        IDownloadQueueService queue,
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        YouTubeUrlNormalizer urlNormalizer,
        IBinaryLocator binaryLocator,
        DownloadFolderService downloadFolderService)
    {
        _queue = queue;
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _urlNormalizer = urlNormalizer;
        _binaryLocator = binaryLocator;
        _downloadFolderService = downloadFolderService;
    }

    public async Task<EnqueueResult> TryEnqueueAsync(
        string url,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var normalized = _urlNormalizer.Normalize(url);
        if (!normalized.IsSuccess || string.IsNullOrWhiteSpace(normalized.NormalizedUrl))
            return EnqueueResult.Failure(normalized.Error ?? "Invalid URL.");

        try
        {
            var config = await _appConfigStore.LoadAsync(cancellationToken);
            if (_binaryLocator.ResolveYtDlpPath(config) is null)
                return EnqueueResult.Failure("yt-dlp was not found. Use Install yt-dlp or set its path in Settings.");

            if (string.IsNullOrWhiteSpace(profileId))
                return EnqueueResult.Failure("No download profile selected. Create one in Settings → Profiles.");

            var profile = await _profileStore.GetAsync(profileId, cancellationToken);
            if (profile is null)
                return EnqueueResult.Failure($"Profile '{profileId}' was not found. Open Settings and save a profile.");

            if (!_downloadFolderService.IsConfigured(config))
                return EnqueueResult.Failure(
                    "Download folder is not set. Open Settings → Queue or restart the app to choose a folder.");

            if (ProfileFfmpegRequirement.RequiresFfmpeg(profile)
                && _binaryLocator.ResolveFfmpegPath(config) is null)
                return EnqueueResult.Failure(ProfileFfmpegRequirement.FfmpegRequiredMessage);

            var job = await _queue.EnqueueAsync(normalized.NormalizedUrl, profileId, cancellationToken);
            return EnqueueResult.Success(job);
        }
        catch (InvalidOperationException ex)
        {
            return EnqueueResult.Failure(ex.Message);
        }
        catch (IOException ex)
        {
            return EnqueueResult.Failure(ex.Message);
        }
    }
}
