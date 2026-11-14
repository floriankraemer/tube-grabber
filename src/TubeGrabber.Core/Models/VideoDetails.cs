// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class VideoDetails
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Channel { get; init; }
    public string? ChannelUrl { get; init; }
    public string? Description { get; init; }
    public long? ViewCount { get; init; }
    public DateOnly? UploadDate { get; init; }
    public int? DurationSeconds { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? VideoStreamUrl { get; init; }
    public string? AudioStreamUrl { get; init; }
}
