// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.Services;

public static class QueueStatusMessageBuilder
{
    /// <summary>Status bar text; empty when there is nothing to report (the page subtitle and empty state carry the hint).</summary>
    public static string Build(IReadOnlyList<DownloadJob> jobs)
    {
        if (jobs.Count == 0)
            return string.Empty;

        var running = jobs.Count(j => j.Status == DownloadStatus.Running);
        var queued = jobs.Count(j => j.Status == DownloadStatus.Queued);
        var failedCount = jobs.Count(j => j.Status == DownloadStatus.Failed);

        if (running > 0)
            return running == 1 ? "Downloading…" : $"{running} downloads running…";

        if (queued > 0)
            return queued == 1 ? "1 item queued (starting…)" : $"{queued} items queued (starting…)";

        if (failedCount > 0)
            return failedCount == 1 ? "Download failed." : $"{failedCount} downloads failed.";

        if (jobs.All(j => j.Status == DownloadStatus.Completed))
            return jobs.Count == 1 ? "Download completed." : "All downloads completed.";

        return string.Empty;
    }

    /// <summary>Downloads page subtitle, e.g. "2 downloading, 1 waiting. Saving to D:\Videos".</summary>
    public static string BuildSubtitle(IReadOnlyList<DownloadJob> jobs, string? downloadFolder)
    {
        var running = jobs.Count(j => j.Status == DownloadStatus.Running);
        var queued = jobs.Count(j => j.Status == DownloadStatus.Queued);

        var parts = new List<string>();
        if (running > 0)
            parts.Add($"{running} downloading");
        if (queued > 0)
            parts.Add($"{queued} waiting");

        var summary = parts.Count > 0
            ? string.Join(", ", parts)
            : "Paste a video or playlist URL to add it to the queue";

        return string.IsNullOrWhiteSpace(downloadFolder)
            ? $"{summary}."
            : $"{summary}. Saving to {downloadFolder}";
    }
}
