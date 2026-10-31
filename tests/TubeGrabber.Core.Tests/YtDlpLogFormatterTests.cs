// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpLogFormatterTests
{
    [Fact]
    public void Format_IncludesCommandOutputAndExitCode()
    {
        var invocation = new YtDlpInvocation
        {
            ExecutablePath = "/usr/bin/yt-dlp",
            Arguments = ["--version"],
        };

        var result = new YtDlpRunResult
        {
            ExitCode = 1,
            StandardOutput = "line one",
            StandardError = "error line",
        };

        var log = YtDlpLogFormatter.Format(invocation, result);

        Assert.Contains("yt-dlp", log);
        Assert.Contains("--- stdout ---", log);
        Assert.Contains("line one", log);
        Assert.Contains("--- stderr ---", log);
        Assert.Contains("error line", log);
        Assert.Contains("[exit code: 1]", log);
    }

    [Fact]
    public void Format_Exception_IncludesCommandAndStack()
    {
        var invocation = new YtDlpInvocation
        {
            ExecutablePath = "yt-dlp.exe",
            Arguments = ["https://example.com/watch?v=abc"],
        };

        var log = YtDlpLogFormatter.Format(invocation, new InvalidOperationException("profile missing"));

        Assert.Contains("yt-dlp.exe", log);
        Assert.Contains("profile missing", log);
    }
}
