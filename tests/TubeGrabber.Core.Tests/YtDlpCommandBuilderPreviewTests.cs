// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpCommandBuilderPreviewTests
{
    [Fact]
    public void BuildPreview_EscapesArgumentsWithSpaces()
    {
        var builder = new YtDlpCommandBuilder(new YtDlpOptionCatalog(), new ExtraArgsTokenizer());
        var profile = new DownloadProfile
        {
            Id = "p",
            Name = "P",
            Options = new Dictionary<string, object?> { ["-o"] = "my videos/%(title)s.%(ext)s" },
        };

        var preview = builder.BuildPreview(profile, null, "https://example.com/v");
        Assert.Contains("\"my videos/%(title)s.%(ext)s\"", preview);
    }
}
