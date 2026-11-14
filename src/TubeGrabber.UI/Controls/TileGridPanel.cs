// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Controls;

/// <summary>Lays children out in rows of equal-width tiles; the column count follows the width (see <see cref="TileGridLayout"/>).</summary>
public sealed class TileGridPanel : Panel
{
    public static readonly StyledProperty<double> MinTileWidthProperty =
        AvaloniaProperty.Register<TileGridPanel, double>(nameof(MinTileWidth), 240);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TileGridPanel, double>(nameof(Spacing), 16);

    static TileGridPanel()
    {
        AffectsMeasure<TileGridPanel>(MinTileWidthProperty, SpacingProperty);
    }

    public double MinTileWidth
    {
        get => GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = TileGridLayout.Columns(availableSize.Width, MinTileWidth, Spacing);
        var tileWidth = TileGridLayout.TileWidth(availableSize.Width, columns, Spacing);
        var height = 0.0;
        foreach (var row in Children.Where(c => c.IsVisible).Chunk(columns))
        {
            var rowHeight = 0.0;
            foreach (var child in row)
            {
                child.Measure(new Size(tileWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }

            height += rowHeight + Spacing;
        }

        if (height > 0)
            height -= Spacing;

        var width = double.IsInfinity(availableSize.Width) ? tileWidth * columns : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = TileGridLayout.Columns(finalSize.Width, MinTileWidth, Spacing);
        var tileWidth = TileGridLayout.TileWidth(finalSize.Width, columns, Spacing);
        var y = 0.0;
        foreach (var row in Children.Where(c => c.IsVisible).Chunk(columns))
        {
            var rowHeight = row.Max(c => c.DesiredSize.Height);
            for (var i = 0; i < row.Length; i++)
                row[i].Arrange(new Rect(TileGridLayout.TileLeft(i, columns, finalSize.Width, tileWidth, Spacing), y, tileWidth, rowHeight));

            y += rowHeight + Spacing;
        }

        return finalSize;
    }
}
