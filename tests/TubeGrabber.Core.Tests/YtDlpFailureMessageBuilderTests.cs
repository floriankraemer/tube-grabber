// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpFailureMessageBuilderTests
{
    [Fact]
    public void Build_PrefersErrorInStdoutOverWarningInStderr()
    {
        var result = new YtDlpRunResult
        {
            ExitCode = 1,
            StandardOutput = "ERROR: Postprocessing: audio conversion failed",
            StandardError = "WARNING: [youtube] No supported JavaScript runtime could be found.",
        };

        var message = YtDlpFailureMessageBuilder.Build(result);

        Assert.Contains("audio conversion failed", message, StringComparison.Ordinal);
        Assert.DoesNotContain("JavaScript runtime", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ReturnsExitCodeWhenStreamsEmpty()
    {
        var message = YtDlpFailureMessageBuilder.Build(new YtDlpRunResult { ExitCode = 2 });
        Assert.Contains("exited with code 2", message, StringComparison.Ordinal);
    }
}
