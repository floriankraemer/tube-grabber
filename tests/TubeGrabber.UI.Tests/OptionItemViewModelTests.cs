// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class OptionItemViewModelTests
{
    [Fact]
    public void BoolValue_UpdatesValue()
    {
        var def = new YtDlpOptionDefinition
        {
            Flag = "--simulate",
            Section = "Verbosity",
            ValueType = "bool",
            Tooltip = "Simulate",
        };

        var item = new OptionItemViewModel(def, false);
        item.BoolValue = true;
        Assert.True(item.BoolValue);
        Assert.Equal(true, item.Value);
    }

    private static YtDlpOptionDefinition Definition(string valueType, string? @default, string label = "") => new()
    {
        Flag = "--opt",
        Label = label,
        Section = "S",
        ValueType = valueType,
        DefaultValue = @default,
        Tooltip = "Describes the option.",
    };

    [Fact]
    public void Label_FallsBackToTheFlag()
    {
        Assert.Equal("--opt", new OptionItemViewModel(Definition("string", ""), null).Label);
        Assert.Equal("Nice name", new OptionItemViewModel(Definition("string", "", "Nice name"), null).Label);
        Assert.Equal("Nice name (--opt)", new OptionItemViewModel(Definition("string", "", "Nice name"), null).AutomationName);
    }

    [Fact]
    public void IsChanged_FollowsTheValue_AndRaisesNotifications()
    {
        var item = new OptionItemViewModel(Definition("int", "10"), null);
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.False(item.IsChanged);
        item.StringValue = "3";

        Assert.True(item.IsChanged);
        Assert.Contains(nameof(OptionItemViewModel.IsChanged), raised);
        Assert.Equal("Changed from default", item.ChangedHelp);
    }

    [Fact]
    public void Reset_StoresTheDefaultExplicitly_AndRefreshesTheBoundValues()
    {
        var item = new OptionItemViewModel(Definition("bool", "false"), true);
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Reset();

        Assert.Equal(false, item.Value);
        Assert.False(item.IsChanged);
        Assert.Contains(nameof(OptionItemViewModel.BoolValue), raised);
        Assert.Contains(nameof(OptionItemViewModel.StringValue), raised);
        Assert.Equal(string.Empty, item.ChangedHelp);
    }

    [Fact]
    public void Constructor_UnwrapsJsonElementValues()
    {
        var element = System.Text.Json.JsonDocument.Parse("true").RootElement;

        var item = new OptionItemViewModel(Definition("bool", "false"), element);

        Assert.True(item.BoolValue);
        Assert.True(item.IsChanged);
    }

    [Theory]
    [InlineData("subtitle", true)]
    [InlineData("NICE", true)]
    [InlineData("--op", true)]
    [InlineData("describes", true)]
    [InlineData("nothing", false)]
    public void Matches_ChecksLabelFlagAndDescription(string query, bool expected) =>
        Assert.Equal(expected, new OptionItemViewModel(Definition("string", "", "Nice subtitle name"), null).Matches(query));
}
