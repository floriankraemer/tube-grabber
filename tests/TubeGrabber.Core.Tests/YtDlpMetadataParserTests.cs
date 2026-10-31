// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpMetadataParserTests
{
    private readonly YtDlpMetadataParser _parser = new();

    [Fact]
    public void TryApplyMetadata_ParsesPrintLines()
    {
        var job = new DownloadJob { Url = "https://example.com", ProfileId = "default" };

        Assert.True(_parser.TryApplyMetadata(job, "tube-grabber-channel:Sample Channel"));
        Assert.True(_parser.TryApplyMetadata(job, "tube-grabber-title:Sample title"));

        Assert.Equal("Sample Channel", job.Channel);
        Assert.Equal("Sample title", job.Title);
    }

    [Fact]
    public void TryApplyMetadata_ParsesJsonFallback()
    {
        var job = new DownloadJob { Url = "https://example.com", ProfileId = "default" };

        Assert.True(_parser.TryApplyMetadata(job, "{\"channel\": \"Channel\", \"title\": \"Title\"}"));

        Assert.Equal("Channel", job.Channel);
        Assert.Equal("Title", job.Title);
    }

    [Fact]
    public void TryApplyMetadata_IgnoresNaValues()
    {
        var job = new DownloadJob { Url = "https://example.com", ProfileId = "default" };

        Assert.False(_parser.TryApplyMetadata(job, "tube-grabber-channel:NA"));
        Assert.True(_parser.TryApplyMetadata(job, "tube-grabber-title:Only title"));

        Assert.Null(job.Channel);
        Assert.Equal("Only title", job.Title);
    }

    [Fact]
    public void FormatQueueTitle_ShowsChannelAndTitle()
    {
        var job = new DownloadJob
        {
            Url = "https://example.com",
            ProfileId = "default",
            Channel = "Channel",
            Title = "Title",
        };

        Assert.Equal("Channel · Title", YtDlpMetadataParser.FormatQueueTitle(job));
    }

    [Fact]
    public void FormatQueueTitle_FallsBackToTitleThenUrl()
    {
        var titleOnly = new DownloadJob
        {
            Url = "https://example.com/watch?v=1",
            ProfileId = "default",
            Title = "Title",
        };
        var urlOnly = new DownloadJob
        {
            Url = "https://example.com/watch?v=1",
            ProfileId = "default",
        };

        Assert.Equal("Title", YtDlpMetadataParser.FormatQueueTitle(titleOnly));
        Assert.Equal(urlOnly.Url, YtDlpMetadataParser.FormatQueueTitle(urlOnly));
    }

    [Fact]
    public void BuildPrintArguments_IncludesBothTemplates()
    {
        var args = YtDlpMetadataParser.BuildPrintArguments();

        Assert.Equal(5, args.Count);
        Assert.Equal("--no-simulate", args[0]);
        Assert.Equal("--print", args[1]);
        Assert.Equal(YtDlpMetadataParser.ChannelPrintTemplate, args[2]);
        Assert.Equal("--print", args[3]);
        Assert.Equal(YtDlpMetadataParser.TitlePrintTemplate, args[4]);
        Assert.StartsWith("before_dl:", YtDlpMetadataParser.ChannelPrintTemplate, StringComparison.Ordinal);
        Assert.StartsWith("before_dl:", YtDlpMetadataParser.TitlePrintTemplate, StringComparison.Ordinal);
    }
}
