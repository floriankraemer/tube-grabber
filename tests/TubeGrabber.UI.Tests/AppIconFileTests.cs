// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class AppIconFileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("icon-tests-").FullName;

    [Fact]
    public void AppIconFile_WritesIconOnceAndReturnsPath()
    {
        var calls = 0;
        Stream Open()
        {
            calls++;
            return new MemoryStream([1, 2, 3]);
        }

        var first = AppIconFile.EnsureWritten(Path.Combine(_root, "cfg"), Open);
        var second = AppIconFile.EnsureWritten(Path.Combine(_root, "cfg"), Open);

        Assert.Equal(first, second);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(first));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AppIconFile_WhenWriteFails_ReturnsEmptyString()
    {
        var result = AppIconFile.EnsureWritten(_root, () => throw new IOException("boom"));

        Assert.Equal(string.Empty, result);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
