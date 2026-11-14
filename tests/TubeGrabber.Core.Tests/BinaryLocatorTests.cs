// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class BinaryLocatorTests
{
    [Fact]
    public void ResolveYtDlpPath_PrefersConfiguredPath()
    {
        var root = Path.GetTempPath();
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "x");
            var locator = TestBinaryLocator.Create(root);
            var config = new AppConfiguration { YtDlpPath = file };
            Assert.Equal(Path.GetFullPath(file), locator.ResolveYtDlpPath(config));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveYtDlpAndFfmpegPath_FallBackToInjectedPathLookup()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var locator = new BinaryLocator(root, name => Path.Combine("fake-bin", name));

        Assert.Equal(Path.Combine("fake-bin", OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp"),
            locator.ResolveYtDlpPath(new AppConfiguration()));
        Assert.Equal(Path.Combine("fake-bin", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"),
            locator.ResolveFfmpegPath(new AppConfiguration()));
    }
}
