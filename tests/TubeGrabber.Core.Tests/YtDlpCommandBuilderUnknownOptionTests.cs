// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpCommandBuilderUnknownOptionTests
{
    [Fact]
    public void Build_SkipsUnknownOptionKeys()
    {
        var builder = new YtDlpCommandBuilder(new YtDlpOptionCatalog(), new ExtraArgsTokenizer());
        var profile = new DownloadProfile
        {
            Id = "p",
            Name = "P",
            Options = new Dictionary<string, object?> { ["--not-in-catalog"] = true },
        };

        var args = builder.Build(profile, null, "https://example.com/v");
        Assert.DoesNotContain("--not-in-catalog", args);
    }
}
