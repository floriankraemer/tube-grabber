// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.Controls;

/// <summary>
/// The visible track of the seek bar, drawn under a transparent Slider so its thumb stays on top: the unplayed rail,
/// the played part, and the SponsorBlock segments (SponsorBlock green) on the time axis.
/// </summary>
public sealed class SeekTrack : Control
{
    private static readonly IBrush SegmentBrush = new SolidColorBrush(Color.Parse("#00D400"));

    public static readonly StyledProperty<IReadOnlyList<SponsorSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<SeekTrack, IReadOnlyList<SponsorSegment>?>(nameof(Segments));

    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<SeekTrack, double>(nameof(Length));

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<SeekTrack, double>(nameof(Position));

    public static readonly StyledProperty<IBrush?> RailBrushProperty =
        AvaloniaProperty.Register<SeekTrack, IBrush?>(nameof(RailBrush));

    public static readonly StyledProperty<IBrush?> PlayedBrushProperty =
        AvaloniaProperty.Register<SeekTrack, IBrush?>(nameof(PlayedBrush));

    static SeekTrack()
    {
        AffectsRender<SeekTrack>(SegmentsProperty, LengthProperty, PositionProperty, RailBrushProperty, PlayedBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SeekTrack>(false);
        HeightProperty.OverrideDefaultValue<SeekTrack>(4);
    }

    public IReadOnlyList<SponsorSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>Total media length in seconds.</summary>
    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <summary>Playback position in seconds.</summary>
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public IBrush? RailBrush
    {
        get => GetValue(RailBrushProperty);
        set => SetValue(RailBrushProperty, value);
    }

    public IBrush? PlayedBrush
    {
        get => GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var radius = height / 2;
        context.DrawRectangle(RailBrush, null, new RoundedRect(new Rect(0, 0, width, height), radius));
        if (Length <= 0)
            return;

        var played = Math.Clamp(Position / Length, 0, 1) * width;
        if (played > 0)
            context.DrawRectangle(PlayedBrush, null, new RoundedRect(new Rect(0, 0, played, height), radius));

        if (Segments is not { Count: > 0 } segments)
            return;

        foreach (var segment in segments)
        {
            var left = Math.Clamp(segment.StartSeconds / Length, 0, 1) * width;
            var right = Math.Clamp(segment.EndSeconds / Length, 0, 1) * width;
            if (right > left)
                context.DrawRectangle(SegmentBrush, null, new RoundedRect(new Rect(left, 0, right - left, height), radius));
        }
    }
}
