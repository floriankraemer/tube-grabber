// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

// Mutates the process-wide PATH, so it must not overlap with any other test class.
[CollectionDefinition(nameof(BinaryLocatorPathTests), DisableParallelization = true)]
public sealed class PathMutationCollection;

[Collection(nameof(BinaryLocatorPathTests))]
public sealed class BinaryLocatorPathTests : IDisposable
{
    private readonly string? _previousPath;
    private readonly string _folder;

    public BinaryLocatorPathTests()
    {
        _previousPath = Environment.GetEnvironmentVariable("PATH");
        _folder = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-path", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "yt-dlp"), string.Empty);
        Environment.SetEnvironmentVariable("PATH", _folder);
    }

    [Fact]
    public void ResolveYtDlpPath_FindsOnPath()
    {
        var locator = new BinaryLocator(Path.GetTempPath());
        var path = locator.ResolveYtDlpPath(new AppConfiguration());
        Assert.Equal(Path.GetFullPath(Path.Combine(_folder, "yt-dlp")), path);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _previousPath);
        if (Directory.Exists(_folder))
            Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);
    }
}
