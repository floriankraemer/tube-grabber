// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class DownloadQueueServiceExceptionTests : IDisposable
{
    private readonly string _root;
    private readonly DownloadQueueService _queue;

    public DownloadQueueServiceExceptionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
        var catalog = new YtDlpOptionCatalog();
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);

        _queue = new DownloadQueueService(
            new ThrowingRunner(),
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            appConfig,
            profiles,
            new BinaryLocator(_root),
            new YouTubeUrlNormalizer(),
            new YtDlpProgressParser(),
            new YtDlpOutputPathParser(),
            new YtDlpMetadataParser(),
            new DownloadFolderService(),
            new JsRuntimeLocator());
    }

    [Fact]
    public async Task EnqueueAsync_WhenRunnerThrows_MarksFailed()
    {
        await new AppConfigStore(_root, new ProfileStore(_root)).EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await new AppConfigStore(_root, new ProfileStore(_root)).LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        config.DownloadFolder = _root;
        await new AppConfigStore(_root, new ProfileStore(_root)).SaveAsync(config, TestContext.Current.CancellationToken);

        var job = await _queue.EnqueueAsync("https://www.youtube.com/watch?v=abc", config.ActiveProfileId, TestContext.Current.CancellationToken);
        await WaitForTerminalStateAsync(job.Id);
        Assert.Equal(DownloadStatus.Failed, _queue.Jobs.Single(j => j.Id == job.Id).Status);
    }

    private async Task WaitForTerminalStateAsync(Guid jobId)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = _queue.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job is { Status: DownloadStatus.Failed or DownloadStatus.Completed or DownloadStatus.Cancelled })
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class ThrowingRunner : IYtDlpProcessRunner
    {
        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("runner exploded");
    }
}
