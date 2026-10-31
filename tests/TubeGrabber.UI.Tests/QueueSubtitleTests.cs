// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class QueueSubtitleTests
{
    private static DownloadJob Job(DownloadStatus status) =>
        new() { Url = "https://example.com", ProfileId = "default", Status = status };

    [Fact]
    public void BuildSubtitle_ListsActiveCountsAndFolder()
    {
        DownloadJob[] jobs =
        [
            Job(DownloadStatus.Running), Job(DownloadStatus.Running), Job(DownloadStatus.Queued), Job(DownloadStatus.Completed),
        ];

        Assert.Equal("2 downloading, 1 waiting. Saving to /videos", QueueStatusMessageBuilder.BuildSubtitle(jobs, "/videos"));
    }

    [Fact]
    public void BuildSubtitle_FallsBackToHintWithoutActiveJobsOrFolder()
    {
        Assert.Equal(
            "Paste a video or playlist URL to add it to the queue.",
            QueueStatusMessageBuilder.BuildSubtitle([], null));
    }

    [Fact]
    public void Build_IsEmptyForAnEmptyQueue_SoTheStatusBarDoesNotRepeatTheSubtitleHint()
    {
        Assert.Equal(string.Empty, QueueStatusMessageBuilder.Build([]));
    }

    [Fact]
    public void Build_ReportsRunningDownloads()
    {
        Assert.Equal("2 downloads running…", QueueStatusMessageBuilder.Build([Job(DownloadStatus.Running), Job(DownloadStatus.Running)]));
    }
}
