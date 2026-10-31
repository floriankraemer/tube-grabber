// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class DownloadOutputResolverTests : IDisposable
{
    private readonly string _root;

    public DownloadOutputResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-output-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Resolve_SingleExistingFile_OpensFile()
    {
        var filePath = Path.Combine(_root, "video.mp4");
        File.WriteAllText(filePath, "data");

        var job = CreateCompletedJob(filePath);

        var target = DownloadOutputResolver.Resolve(job);

        Assert.True(target.CanOpen);
        Assert.True(target.IsSingleFile);
        Assert.Equal(filePath, target.Path);
    }

    [Fact]
    public void Resolve_MultipleExistingFiles_OpensLocation()
    {
        var first = Path.Combine(_root, "one.mp4");
        var second = Path.Combine(_root, "two.mp4");
        File.WriteAllText(first, "a");
        File.WriteAllText(second, "b");

        var job = CreateCompletedJob();
        job.OutputPaths.Add(first);
        job.OutputPaths.Add(second);

        var target = DownloadOutputResolver.Resolve(job);

        Assert.True(target.CanOpen);
        Assert.False(target.IsSingleFile);
        Assert.Equal(_root, target.Path);
    }

    [Fact]
    public void Resolve_NoOutputPaths_FallsBackToWorkingDirectory()
    {
        var job = CreateCompletedJob();
        job.WorkingDirectory = _root;

        var target = DownloadOutputResolver.Resolve(job);

        Assert.True(target.CanOpen);
        Assert.False(target.IsSingleFile);
        Assert.Equal(_root, target.Path);
    }

    [Fact]
    public void Resolve_IncompleteJob_CannotOpen()
    {
        var job = new DownloadJob
        {
            Url = "https://example.com",
            ProfileId = "default",
            Status = DownloadStatus.Running,
            WorkingDirectory = _root,
        };
        job.OutputPaths.Add(Path.Combine(_root, "video.mp4"));

        Assert.False(DownloadOutputResolver.CanOpen(job));
    }

    private DownloadJob CreateCompletedJob(string? outputPath = null)
    {
        var job = new DownloadJob
        {
            Url = "https://example.com",
            ProfileId = "default",
            Status = DownloadStatus.Completed,
            WorkingDirectory = _root,
        };

        if (outputPath is not null)
            job.OutputPaths.Add(outputPath);

        return job;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
