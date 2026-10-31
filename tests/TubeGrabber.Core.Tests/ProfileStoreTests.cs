// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileStore _store;

    public ProfileStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _store = new ProfileStore(_root);
    }

    [Fact]
    public async Task ConcurrentSavesAndReads_NeverSeeLockedOrPartialFile()
    {
        var profile = new DownloadProfile { Id = "p1", Name = "Music" };
        await _store.SaveAsync(profile, TestContext.Current.CancellationToken);

        var tasks = Enumerable.Range(0, 64)
            .Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0)
                    await _store.SaveAsync(new DownloadProfile { Id = "p1", Name = "Music" });
                else
                    Assert.Equal("Music", (await _store.GetAsync("p1"))?.Name);
            }));

        await Task.WhenAll(tasks);

        Assert.Single(Directory.GetFiles(Path.Combine(_root, AppPaths.ProfilesFolderName)));
    }

    [Fact]
    public async Task SaveAndGet_RoundTripsProfile()
    {
        var profile = new DownloadProfile
        {
            Id = "p1",
            Name = "Music",
            Options = new Dictionary<string, object?> { ["-f"] = "bestaudio" },
            ExtraArgs = "--simulate",
        };

        await _store.SaveAsync(profile, TestContext.Current.CancellationToken);
        var loaded = await _store.GetAsync("p1", TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal("Music", loaded.Name);
        Assert.Equal("--simulate", loaded.ExtraArgs);
    }

    [Fact]
    public async Task DeleteAsync_BlocksLastProfile()
    {
        await _store.SaveAsync(new DownloadProfile { Id = AppPaths.DefaultProfileId, Name = "Default" }, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.DeleteAsync(AppPaths.DefaultProfileId, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
