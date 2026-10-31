// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class DownloadFolderServiceTests : IDisposable
{
    private readonly string _root;
    private readonly DownloadFolderService _service = new();

    public DownloadFolderServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void GetSuggestedDefaultPath_UsesHomeAndSubfolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var suggested = DownloadFolderService.GetSuggestedDefaultPath();

        Assert.StartsWith(home, suggested, StringComparison.Ordinal);
        Assert.EndsWith(AppPaths.DefaultDownloadFolderName, suggested, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveWorkingDirectory_UsesConfiguredDownloadFolder()
    {
        var config = new AppConfiguration { DownloadFolder = _root };
        var profile = new DownloadProfile { Id = "default", Name = "Default" };

        var resolved = _service.ResolveWorkingDirectory(config, profile);

        Assert.Equal(Path.GetFullPath(_root), resolved);
        Assert.True(Directory.Exists(resolved));
    }

    [Fact]
    public void ResolveWorkingDirectory_ProfilePathOverridesConfig()
    {
        var profileDir = Path.Combine(_root, "profile-output");
        var config = new AppConfiguration { DownloadFolder = _root };
        var profile = new DownloadProfile
        {
            Id = "default",
            Name = "Default",
            Options = new Dictionary<string, object?> { ["-P"] = profileDir },
        };

        var resolved = _service.ResolveWorkingDirectory(config, profile);

        Assert.Equal(Path.GetFullPath(profileDir), resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
