// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class VideoComment
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public required string Text { get; init; }
    public long? LikeCount { get; init; }
    public DateTimeOffset? PostedAt { get; init; }
    public bool IsPinned { get; init; }
    public bool AuthorIsUploader { get; init; }
}
