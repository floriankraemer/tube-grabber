// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class DownloadJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Url { get; init; }
    public string? Channel { get; set; }
    public string? Title { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public double Progress { get; set; }
    public DownloadProgressPhase ProgressPhase { get; set; } = DownloadProgressPhase.Downloading;
    public string? ProgressActivity { get; set; }
    public bool UseIndeterminateProgress { get; set; }
    public string? Speed { get; set; }
    public string? Eta { get; set; }
    public string? TotalSize { get; set; }
    public string? Error { get; set; }
    public string? LogOutput { get; set; }
    public string? WorkingDirectory { get; set; }
    public List<string> OutputPaths { get; } = [];
    public required string ProfileId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
