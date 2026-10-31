// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpOptionCatalogTests
{
    [Fact]
    public void LoadEmbedded_ContainsCommonFlags()
    {
        var catalog = new YtDlpOptionCatalog();
        Assert.NotNull(catalog.FindByFlag("-f"));
        Assert.NotEmpty(catalog.GetSections());
    }

    [Fact]
    public void EveryOption_HasAHumanLabel()
    {
        foreach (var option in new YtDlpOptionCatalog().GetAll())
            Assert.False(string.IsNullOrWhiteSpace(option.Label), option.Flag);
    }
}
