// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class QueueColumnLayoutTests
{
    [Fact]
    public void Capture_ReturnsWidthForEachKnownColumn()
    {
        var grid = CreateSampleGrid();

        var captured = QueueColumnLayout.Capture(grid);

        Assert.Equal(4, captured.Count);
        Assert.Equal(90, captured["status"]);
        Assert.Equal(200, captured["actions"]);
    }

    [Fact]
    public void Apply_UsesSavedWidthsWhenProvided()
    {
        var grid = CreateSampleGrid();
        var saved = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["title"] = 400,
            ["progress"] = 300,
        };

        QueueColumnLayout.Apply(grid, saved);

        Assert.Equal(400, WidthOf(grid, "title"));
        Assert.Equal(300, WidthOf(grid, "progress"));
        Assert.Equal(90, WidthOf(grid, "status"));
    }

    [Fact]
    public void Apply_IgnoresLayoutsSavedWithObsoleteColumnKeys()
    {
        var grid = CreateSampleGrid();
        var saved = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["status"] = 100,
            ["title"] = 150,
            ["url"] = 150,
            ["speed"] = 75,
            ["actions"] = 190,
        };

        QueueColumnLayout.Apply(grid, saved);

        Assert.Equal(90, WidthOf(grid, "status"));
        Assert.Equal(220, WidthOf(grid, "title"));
        Assert.Equal(200, WidthOf(grid, "actions"));
    }

    [Fact]
    public void Apply_IgnoresEmptyOrMissingLayout()
    {
        var grid = CreateSampleGrid();

        QueueColumnLayout.Apply(grid, null);
        QueueColumnLayout.Apply(grid, new Dictionary<string, double>());

        Assert.Equal(220, WidthOf(grid, "title"));
    }

    private static double WidthOf(DataGrid grid, string key) =>
        grid.Columns.First(c => QueueColumnLayout.GetLayoutKey(c) == key).Width.DisplayValue;

    private static DataGrid CreateSampleGrid()
    {
        var grid = new DataGrid { AutoGenerateColumns = false };
        grid.Columns.Add(CreateColumn<DataGridTemplateColumn>("status", 90));
        grid.Columns.Add(CreateColumn<DataGridTemplateColumn>("title", 220));
        grid.Columns.Add(CreateColumn<DataGridTemplateColumn>("progress", 140));
        grid.Columns.Add(CreateColumn<DataGridTemplateColumn>("actions", 200));
        return grid;
    }

    private static T CreateColumn<T>(string key, double width)
        where T : DataGridColumn, new()
    {
        var column = new T { Width = Pixels(width) };
        QueueColumnLayout.SetLayoutKey(column, key);
        return column;
    }

    private static DataGridLength Pixels(double value) =>
        new(value, DataGridLengthUnitType.Pixel);
}
