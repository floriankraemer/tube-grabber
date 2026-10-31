// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpCommandBuilderTests
{
    private readonly YtDlpCommandBuilder _builder = new(new YtDlpOptionCatalog(), new ExtraArgsTokenizer());

    [Fact]
    public void Build_IncludesFfmpegAndUrl()
    {
        var profile = new DownloadProfile
        {
            Id = "test",
            Name = "Test",
            Options = new Dictionary<string, object?> { ["-f"] = "best" },
        };

        var args = _builder.Build(profile, "/usr/bin/ffmpeg", "https://example.com/v");
        Assert.Contains("--ffmpeg-location", args);
        Assert.Contains("/usr/bin/ffmpeg", args);
        Assert.Equal("https://example.com/v", args[^1]);
    }

    [Fact]
    public void Build_AppendsExtraArgsTokens()
    {
        var profile = new DownloadProfile
        {
            Id = "test",
            Name = "Test",
            ExtraArgs = "--simulate --verbose",
        };

        var args = _builder.Build(profile, null, "https://example.com/v");
        Assert.Contains("--simulate", args);
        Assert.Contains("--verbose", args);
    }

    [Fact]
    public void Build_BoolFlagOnlyWhenTrue()
    {
        var profile = new DownloadProfile
        {
            Id = "test",
            Name = "Test",
            Options = new Dictionary<string, object?> { ["--write-subs"] = false, ["--embed-subs"] = true },
        };

        var args = _builder.Build(profile, null, "https://example.com/v");
        Assert.DoesNotContain("--write-subs", args);
        Assert.Contains("--embed-subs", args);
    }

    [Fact]
    public void Build_IncludesMetadataPrintArguments()
    {
        var profile = new DownloadProfile { Id = "test", Name = "Test" };
        var args = _builder.Build(profile, null, "https://example.com/v");

        Assert.Contains("--no-simulate", args);
        Assert.Contains("--print", args);
        Assert.Contains(YtDlpMetadataParser.ChannelPrintTemplate, args);
        Assert.Contains(YtDlpMetadataParser.TitlePrintTemplate, args);
    }
}
