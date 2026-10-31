// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class DownloadQueueServiceTests : IDisposable
{
    private readonly string _root;
    private readonly FakeYtDlpProcessRunner _runner = new();
    private readonly DownloadQueueService _queue;

    public DownloadQueueServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
        var catalog = new YtDlpOptionCatalog();
        _queue = new DownloadQueueService(
            _runner,
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

        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Fact]
    public async Task EnqueueAsync_CompletesSuccessfully()
    {
        await new AppConfigStore(_root, new ProfileStore(_root)).EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await new AppConfigStore(_root, new ProfileStore(_root)).LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        config.DownloadFolder = _root;
        await new AppConfigStore(_root, new ProfileStore(_root)).SaveAsync(config, TestContext.Current.CancellationToken);

        File.WriteAllText(config.YtDlpPath!, string.Empty);

        var job = await _queue.EnqueueAsync("https://www.youtube.com/watch?v=abc", config.ActiveProfileId, TestContext.Current.CancellationToken);
        await WaitForCompletionAsync(job.Id);

        var completed = _queue.Jobs.Single(j => j.Id == job.Id);
        Assert.Equal(DownloadStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task EnqueueAsync_InvalidUrl_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _queue.EnqueueAsync(string.Empty, "default", TestContext.Current.CancellationToken));
    }

    private async Task WaitForCompletionAsync(Guid jobId)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = _queue.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job is { Status: DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled })
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("Job did not complete.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeYtDlpProcessRunner : IYtDlpProcessRunner
    {
        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default)
        {
            stdoutProgress?.Report("download:PROGRESS=100%|SPEED=1MiB/s|ETA=00:00");
            return Task.FromResult(new YtDlpRunResult { ExitCode = 0 });
        }
    }
}
