// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.Controls;

/// <summary>A thin strip that marks sponsor segments (SponsorBlock green) along the seek bar's time axis.</summary>
public sealed class SegmentBar : Control
{
    private static readonly IBrush SegmentBrush = new SolidColorBrush(Color.Parse("#00D400"));

    public static readonly StyledProperty<IReadOnlyList<SponsorSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<SegmentBar, IReadOnlyList<SponsorSegment>?>(nameof(Segments));

    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<SegmentBar, double>(nameof(Length));

    static SegmentBar()
    {
        AffectsRender<SegmentBar>(SegmentsProperty, LengthProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SegmentBar>(false);
        HeightProperty.OverrideDefaultValue<SegmentBar>(4);
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

    public override void Render(DrawingContext context)
    {
        if (Segments is not { Count: > 0 } segments || Length <= 0)
            return;

        foreach (var segment in segments)
        {
            var left = Math.Clamp(segment.StartSeconds / Length, 0, 1) * Bounds.Width;
            var right = Math.Clamp(segment.EndSeconds / Length, 0, 1) * Bounds.Width;
            if (right > left)
                context.DrawRectangle(SegmentBrush, null, new RoundedRect(new Rect(left, 0, right - left, Bounds.Height), 2));
        }
    }
}
