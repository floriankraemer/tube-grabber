// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Constants;

public static class AppPaths
{
    public const string ConfigFolderName = ".yt-dlp-ui";
    public const string AppConfigFileName = "app.json";
    public const string ProfilesFolderName = "profiles";
    public const string DefaultProfileId = "default";
    public const string AudioMp3ProfileId = "audio-mp3";
    public const string HqVideoProfileId = "hq-video";
    public const string BinFolderName = "bin";
    public const string DefaultDownloadFolderName = "youtube-downloads";
    public const int DefaultMaxConcurrentDownloads = 2;
    public const int MaxConcurrentDownloadsCap = 8;
    public const int DefaultSubscriptionCheckIntervalMinutes = 60;
    public const int MinSubscriptionCheckIntervalMinutes = 15;
    public const int MaxSubscriptionCheckIntervalMinutes = 1440;
}
