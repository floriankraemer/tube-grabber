// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;

namespace TubeGrabber.Core.Models;

public sealed class ChannelSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string ChannelId { get; set; }
    public required string ChannelName { get; set; }
    public required string ChannelUrl { get; set; }
    public required string ProfileId { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
    public List<string> SeenVideoIds { get; set; } = [];
    public Dictionary<string, int> FailedAttempts { get; set; } = [];

    public void MarkSeen(string videoId)
    {
        FailedAttempts.Remove(videoId);
        if (SeenVideoIds.Contains(videoId))
            return;

        SeenVideoIds.Add(videoId);

        var overflow = SeenVideoIds.Count - AppPaths.MaxSeenVideoIdsPerSubscription;
        if (overflow > 0)
            SeenVideoIds.RemoveRange(0, overflow);
    }
}
