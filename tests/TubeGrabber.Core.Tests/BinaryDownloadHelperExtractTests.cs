// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class BinaryDownloadHelperExtractTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tg-extract-" + Guid.NewGuid().ToString("N"));

    public BinaryDownloadHelperExtractTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ExtractFfmpegFromArchive_UnreadableTarXz_ExplainsWhichToolsAreNeeded()
    {
        var archive = Path.Combine(_dir, "ffmpeg.tar.xz");
        File.WriteAllText(archive, "not an archive");

        var ex = Assert.Throws<InvalidOperationException>(
            () => BinaryDownloadHelper.ExtractFfmpegFromArchive(archive, Path.Combine(_dir, "out")));

        Assert.Contains("xz-utils", ex.Message);
    }
}
