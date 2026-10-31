// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class OptionItemViewModelParsingTests
{
    [Fact]
    public void StringValue_ParsesIntegerOption()
    {
        var item = new OptionItemViewModel(
            new YtDlpOptionDefinition
            {
                Flag = "--retries",
                Section = "Download",
                ValueType = "int",
                Tooltip = "Retries",
            },
            10);

        item.StringValue = "5";
        Assert.Equal(5, item.Value);
    }
}
