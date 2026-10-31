// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;

namespace TubeGrabber.UI.Services;

public sealed class QueueColumnLayout
{
    private QueueColumnLayout()
    {
    }

    public const double MinColumnWidthPixels = 40;

    public static readonly AttachedProperty<string?> LayoutKeyProperty =
        AvaloniaProperty.RegisterAttached<QueueColumnLayout, DataGridColumn, string?>("LayoutKey");

    /// <summary>Keys of the current columns. A saved layout with any other key predates this column set and is ignored.</summary>
    public static readonly IReadOnlySet<string> KnownKeys =
        new HashSet<string>(StringComparer.Ordinal) { "status", "title", "progress", "actions" };

    public static void SetLayoutKey(DataGridColumn column, string? value) =>
        column.SetValue(LayoutKeyProperty, value);

    public static string? GetLayoutKey(DataGridColumn column) =>
        column.GetValue(LayoutKeyProperty);

    public static void Apply(DataGrid grid, IReadOnlyDictionary<string, double>? savedWidths)
    {
        // Empty or obsolete layouts keep the widths declared in XAML.
        if (savedWidths is not { Count: > 0 } widths || widths.Keys.Any(key => !KnownKeys.Contains(key)))
            return;

        foreach (var column in grid.Columns)
        {
            var key = GetLayoutKey(column);
            if (key is null || !widths.TryGetValue(key, out var pixels) || pixels < MinColumnWidthPixels)
                continue;

            column.Width = new DataGridLength(pixels, DataGridLengthUnitType.Pixel);
        }
    }

    public static Dictionary<string, double> Capture(DataGrid grid)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var column in grid.Columns)
        {
            var key = GetLayoutKey(column);
            if (key is null)
                continue;

            var pixels = column.ActualWidth > 0
                ? column.ActualWidth
                : column.Width.IsAbsolute
                    ? column.Width.DisplayValue
                    : 0;

            if (pixels >= MinColumnWidthPixels)
                result[key] = Math.Round(pixels, 1);
        }

        return result;
    }
}
