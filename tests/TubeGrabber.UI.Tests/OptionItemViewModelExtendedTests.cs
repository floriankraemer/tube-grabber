// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class OptionItemViewModelExtendedTests
{
    [Fact]
    public void Constructor_ParsesBoolDefaultTrue()
    {
        var item = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "--simulate",
                Section = "Verbosity",
                ValueType = "bool",
                DefaultValue = "true",
                Tooltip = "Simulate",
            },
            null);

        Assert.True(item.BoolValue);
    }

    [Fact]
    public void StringValue_ParsesDoubleOption()
    {
        var item = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "--sleep-requests",
                Section = "Workarounds",
                ValueType = "double",
                Tooltip = "Sleep",
            },
            null);

        item.StringValue = "1.5";
        Assert.Equal(1.5, item.Value);
    }

    [Fact]
    public void Metadata_ExposesDefinitionFields()
    {
        var item = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "--format",
                Section = "Video Format",
                ValueType = "choice",
                Choices = ["mp4", "webm"],
                Tooltip = "Output format",
                DefaultValue = "mp4",
            },
            null);

        Assert.Equal("--format", item.Flag);
        Assert.Equal("Output format", item.Tooltip);
        Assert.Equal("choice", item.ValueType);
        Assert.Equal(["mp4", "webm"], item.Choices);
        Assert.True(item.IsChoice);
        Assert.Equal("mp4", item.StringValue);
    }

    [Fact]
    public void StringValue_SetNonNumericValue_StoresRawString()
    {
        var item = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "-o",
                Section = "Filesystem",
                ValueType = "string",
                Tooltip = "Output template",
            },
            "old");

        item.StringValue = "new-template";
        Assert.Equal("new-template", item.Value);
        Assert.Equal("new-template", item.StringValue);
    }

    [Fact]
    public void ParseDefault_ParsesDoubleDefault()
    {
        var item = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "--sleep-requests",
                Section = "Workarounds",
                ValueType = "double",
                DefaultValue = "2.5",
                Tooltip = "Sleep",
            },
            null);

        Assert.Equal(2.5, item.Value);
    }

    [Fact]
    public void IsStringOption_FalseForBoolAndChoice()
    {
        var boolItem = new OptionItemViewModel(
            new YtDlpOptionDefinition { Flag = "-q", Section = "Verbosity", ValueType = "bool", Tooltip = "q" },
            false);
        var choiceItem = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "--merge-output-format",
                Section = "Video Format",
                ValueType = "choice",
                Choices = ["mp4"],
                Tooltip = "fmt",
            },
            "mp4");

        Assert.False(boolItem.IsStringOption);
        Assert.False(choiceItem.IsStringOption);
    }
}
