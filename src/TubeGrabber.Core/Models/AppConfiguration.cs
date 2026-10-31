// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class AppConfiguration
{
    public int SchemaVersion { get; set; } = Constants.SchemaVersions.AppConfig;
    public string? YtDlpPath { get; set; }
    public string? FfmpegPath { get; set; }
    public string? JsRuntimeEngine { get; set; }
    public string? JsRuntimePath { get; set; }
    public string? DownloadFolder { get; set; }
    public int MaxConcurrentDownloads { get; set; } = Constants.AppPaths.DefaultMaxConcurrentDownloads;
    public int SubscriptionCheckIntervalMinutes { get; set; } = Constants.AppPaths.DefaultSubscriptionCheckIntervalMinutes;
    public string ActiveProfileId { get; set; } = Constants.AppPaths.DefaultProfileId;
    public int CatalogVersion { get; set; } = Constants.SchemaVersions.OptionCatalog;
    public string YtDlpReleaseTag { get; set; } = Constants.BinaryReleaseManifest.YtDlpReleaseTag;
    public string FfmpegBuildId { get; set; } = Constants.BinaryReleaseManifest.FfmpegBuildId;
    public Dictionary<string, double> QueueColumnWidths { get; set; } = new(StringComparer.Ordinal);
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;
    public bool RunInBackground { get; set; } = true;
    public bool StartAtLogin { get; set; }
}
