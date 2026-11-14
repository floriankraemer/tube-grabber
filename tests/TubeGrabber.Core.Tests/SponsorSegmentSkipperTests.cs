// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class SponsorSegmentSkipperTests
{
    private static readonly SponsorSegment Sponsor = new("a", "sponsor", 10, 20);

    [Fact]
    public void NextSeekTarget_PositionInsideSegment_SeeksToEndAndReportsUuid()
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(12, [Sponsor], new HashSet<string>());

        Assert.NotNull(result);
        Assert.Equal(20.5, result.SeekTo);
        Assert.Equal(["a"], result.Uuids);
    }

    [Theory]
    [InlineData(9.99)]
    [InlineData(20)]
    [InlineData(25)]
    public void NextSeekTarget_PositionOutsideSegment_ReturnsNull(double position)
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(position, [Sponsor], new HashSet<string>());

        Assert.Null(result);
    }

    [Theory]
    [InlineData(19.9)]
    [InlineData(19.75)]
    public void NextSeekTarget_PositionWithinEndGuard_ReturnsNull(double position)
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(position, [Sponsor], new HashSet<string>());

        Assert.Null(result);
    }

    [Fact]
    public void NextSeekTarget_PositionJustBeforeEndGuard_StillSkips()
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(19.7, [Sponsor], new HashSet<string>());

        Assert.NotNull(result);
        Assert.Equal(20.5, result.SeekTo);
    }

    [Fact]
    public void NextSeekTarget_SegmentAlreadySkipped_ReturnsNull()
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(12, [Sponsor], new HashSet<string> { "a" });

        Assert.Null(result);
    }

    [Fact]
    public void NextSeekTarget_ChainedSegments_JumpsToFurthestEndAndReportsAllUuids()
    {
        var segments = new[]
        {
            new SponsorSegment("a", "sponsor", 10, 20),
            new SponsorSegment("b", "sponsor", 20, 30),
            new SponsorSegment("c", "sponsor", 29, 40),
            new SponsorSegment("far", "sponsor", 50, 60),
        };

        var result = SponsorSegmentSkipper.NextSeekTarget(12, segments, new HashSet<string>());

        Assert.NotNull(result);
        Assert.Equal(40.5, result.SeekTo);
        Assert.Equal(["a", "b", "c"], result.Uuids);
    }

    [Fact]
    public void NextSeekTarget_ChainedSegmentAlreadySkipped_StillJumpsPastIt()
    {
        var segments = new[]
        {
            new SponsorSegment("a", "sponsor", 10, 20),
            new SponsorSegment("b", "sponsor", 15, 30),
        };

        var result = SponsorSegmentSkipper.NextSeekTarget(12, segments, new HashSet<string> { "b" });

        Assert.NotNull(result);
        Assert.Equal(30.5, result.SeekTo);
        Assert.Equal(["a", "b"], result.Uuids);
    }

    [Fact]
    public void NextSeekTarget_NeverLandsInsideTheSegmentItSkips()
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(12, [Sponsor], new HashSet<string>());

        Assert.NotNull(result);
        Assert.True(result.SeekTo > Sponsor.EndSeconds);
        Assert.Equal(Sponsor.EndSeconds + SponsorSegmentSkipper.LandingMarginSeconds, result.SeekTo);
    }

    [Fact]
    public void NextSeekTarget_SegmentStartingWithinTheLandingMargin_IsChained()
    {
        var segments = new[]
        {
            new SponsorSegment("a", "sponsor", 10, 20),
            new SponsorSegment("b", "sponsor", 20.3, 30),
        };

        var result = SponsorSegmentSkipper.NextSeekTarget(12, segments, new HashSet<string>());

        Assert.NotNull(result);
        Assert.Equal(30.5, result.SeekTo);
        Assert.Equal(["a", "b"], result.Uuids);
    }

    [Fact]
    public void NextSeekTarget_NoSegments_ReturnsNull()
    {
        var result = SponsorSegmentSkipper.NextSeekTarget(12, [], new HashSet<string>());

        Assert.Null(result);
    }
}
