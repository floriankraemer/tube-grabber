// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Controls;

namespace YtDlpUi.UI.Services;

public sealed class QueueColumnLayout
{
    private QueueColumnLayout()
    {
    }

    public const double MinColumnWidthPixels = 40;

    public static readonly AttachedProperty<string?> LayoutKeyProperty =
        AvaloniaProperty.RegisterAttached<QueueColumnLayout, DataGridColumn, string?>("LayoutKey");

    public static readonly IReadOnlyDictionary<string, double> DefaultWidths =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["status"] = 100,
            ["title"] = 150,
            ["url"] = 150,
            ["progress"] = 110,
            ["speed"] = 75,
            ["eta"] = 65,
            ["error"] = 100,
            ["actions"] = 190,
        };

    public static void SetLayoutKey(DataGridColumn column, string? value) =>
        column.SetValue(LayoutKeyProperty, value);

    public static string? GetLayoutKey(DataGridColumn column) =>
        column.GetValue(LayoutKeyProperty);

    public static void Apply(DataGrid grid, IReadOnlyDictionary<string, double>? savedWidths)
    {
        var widths = savedWidths is { Count: > 0 } ? savedWidths : DefaultWidths;

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
