// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class AppSettingsValidator
{
    private readonly ExtraArgsTokenizer _extraArgsTokenizer;
    private readonly DownloadFolderService _downloadFolderService;

    public AppSettingsValidator(
        ExtraArgsTokenizer extraArgsTokenizer,
        DownloadFolderService downloadFolderService)
    {
        _extraArgsTokenizer = extraArgsTokenizer;
        _downloadFolderService = downloadFolderService;
    }

    public IReadOnlyList<string> Validate(AppConfiguration config, DownloadProfile profile)
    {
        var errors = new List<string>();

        if (config.MaxConcurrentDownloads < 1 || config.MaxConcurrentDownloads > AppPaths.MaxConcurrentDownloadsCap)
            errors.Add($"Max concurrent downloads must be between 1 and {AppPaths.MaxConcurrentDownloadsCap}.");

        if (config.SubscriptionCheckIntervalMinutes < AppPaths.MinSubscriptionCheckIntervalMinutes
            || config.SubscriptionCheckIntervalMinutes > AppPaths.MaxSubscriptionCheckIntervalMinutes)
            errors.Add($"Subscription check interval must be between {AppPaths.MinSubscriptionCheckIntervalMinutes} and {AppPaths.MaxSubscriptionCheckIntervalMinutes} minutes.");

        if (string.IsNullOrWhiteSpace(profile.Name))
            errors.Add("Profile name is required.");

        if (!string.IsNullOrWhiteSpace(config.YtDlpPath) && !File.Exists(config.YtDlpPath))
            errors.Add("yt-dlp path does not exist.");

        if (!string.IsNullOrWhiteSpace(config.FfmpegPath) && !File.Exists(config.FfmpegPath))
            errors.Add("ffmpeg path does not exist.");

        if (!string.IsNullOrWhiteSpace(config.JsRuntimeEngine)
            && !JsRuntimeEngines.IsSupported(config.JsRuntimeEngine))
            errors.Add("JavaScript runtime engine is not supported.");

        if (!string.IsNullOrWhiteSpace(config.JsRuntimePath) && !File.Exists(config.JsRuntimePath))
            errors.Add("JavaScript runtime path does not exist.");

        if (!string.IsNullOrWhiteSpace(config.DownloadFolder)
            && !_downloadFolderService.TryNormalize(config.DownloadFolder, out _))
            errors.Add("Download folder path is invalid.");

        try
        {
            if (!string.IsNullOrWhiteSpace(profile.ExtraArgs))
                _ = _extraArgsTokenizer.Tokenize(profile.ExtraArgs);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        return errors;
    }
}
