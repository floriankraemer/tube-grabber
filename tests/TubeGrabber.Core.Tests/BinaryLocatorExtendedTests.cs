// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class BinaryLocatorExtendedTests : IDisposable
{
    private readonly string _root;

    public BinaryLocatorExtendedTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "bin", "ffmpeg"));
        File.WriteAllText(Path.Combine(_root, "bin", "ffmpeg", "ffmpeg"), string.Empty);
    }

    [Fact]
    public void ResolveFfmpegPath_UsesBundledBinary()
    {
        var locator = new BinaryLocator(_root);
        var path = locator.ResolveFfmpegPath(new AppConfiguration());
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void GetBundledPaths_ReturnExpectedLocations()
    {
        var locator = new BinaryLocator(_root);
        Assert.Contains("yt-dlp", locator.GetBundledYtDlpPath());
        Assert.Contains("ffmpeg", locator.GetBundledFfmpegPath());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
