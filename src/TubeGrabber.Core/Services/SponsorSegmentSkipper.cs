// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class SponsorSegmentSkipper
{
    // Seeking into the last quarter second of a segment would only skip a sliver and re-trigger on the next tick.
    private const double EndGuardSeconds = 0.25;

    // Returns the furthest seek target reachable from the position, following overlapping or adjacent segments
    // ("chain"), so one jump clears the whole chain. Segment order does not matter.
    // ponytail: linear rescans per call, fine for the handful of segments a video has; index by start if that changes.
    public static SponsorSkip? NextSeekTarget(
        double positionSeconds,
        IReadOnlyList<SponsorSegment> segments,
        ISet<string> alreadySkippedUuids)
    {
        var entry = segments.FirstOrDefault(segment =>
            !alreadySkippedUuids.Contains(segment.Uuid)
            && positionSeconds >= segment.StartSeconds
            && positionSeconds < segment.EndSeconds - EndGuardSeconds);

        if (entry is null)
            return null;

        var seekTo = entry.EndSeconds;
        var chained = new List<string> { entry.Uuid };
        var extended = true;
        while (extended)
        {
            extended = false;
            foreach (var segment in segments)
            {
                if (chained.Contains(segment.Uuid) || segment.StartSeconds > seekTo || segment.EndSeconds <= seekTo)
                    continue;

                seekTo = segment.EndSeconds;
                chained.Add(segment.Uuid);
                extended = true;
            }
        }

        return new SponsorSkip(seekTo, chained);
    }
}
