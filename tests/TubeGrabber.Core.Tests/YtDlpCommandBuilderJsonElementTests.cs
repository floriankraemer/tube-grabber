// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpCommandBuilderJsonElementTests
{
    [Fact]
    public void Build_HandlesJsonElementOptionValues()
    {
        var builder = new YtDlpCommandBuilder(new YtDlpOptionCatalog(), new ExtraArgsTokenizer());
        using var document = JsonDocument.Parse("{\"--write-subs\": true, \"-f\": \"best\"}");
        var options = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            options[property.Name] = property.Value;

        var profile = new DownloadProfile { Id = "p", Name = "P", Options = options };
        var args = builder.Build(profile, null, "https://example.com/v");
        Assert.Contains("--write-subs", args);
        Assert.Contains("best", args);
    }
}
