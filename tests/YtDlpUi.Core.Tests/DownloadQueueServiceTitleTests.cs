// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class DownloadQueueServiceTitleTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileStore _profiles;
    private readonly AppConfigStore _appConfig;
    private readonly DownloadQueueService _queue;

    public DownloadQueueServiceTitleTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        _profiles = new ProfileStore(_root);
        _appConfig = new AppConfigStore(_root, _profiles);
        var catalog = new YtDlpOptionCatalog();
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);

        _queue = new DownloadQueueService(
            new TitleRunner(),
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            _appConfig,
            _profiles,
            new BinaryLocator(_root),
            new YouTubeUrlNormalizer(),
            new YtDlpProgressParser(),
            new YtDlpOutputPathParser(),
            new YtDlpMetadataParser(),
            new DownloadFolderService(),
            new JsRuntimeLocator());
    }

    [Fact]
    public async Task EnqueueAsync_ExtractsTitleFromOutput()
    {
        await _appConfig.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await _appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        config.DownloadFolder = _root;
        await _appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        var job = await _queue.EnqueueAsync("https://www.youtube.com/watch?v=abc", config.ActiveProfileId, TestContext.Current.CancellationToken);
        var completed = await WaitForJobAsync(job.Id, static j =>
            j.Status == DownloadStatus.Completed
            && !string.IsNullOrWhiteSpace(j.Title)
            && !string.IsNullOrWhiteSpace(j.Channel));

        Assert.Equal("Sample Channel", completed.Channel);
        Assert.Equal("Sample title", completed.Title);
    }

    private async Task<DownloadJob> WaitForJobAsync(Guid jobId, Func<DownloadJob, bool> predicate)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = _queue.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job is not null && predicate(job))
                return job;

            if (job is { Status: DownloadStatus.Failed or DownloadStatus.Cancelled })
                throw new InvalidOperationException($"Job ended as {job.Status} with title '{job.Title ?? "(null)"}'.");

            await Task.Delay(50);
        }

        throw new TimeoutException("Job did not reach the expected state.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class TitleRunner : IYtDlpProcessRunner
    {
        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default)
        {
            const string channelLine = "ytdlp-ui-channel:Sample Channel";
            const string titleLine = "ytdlp-ui-title:Sample title";
            stdoutProgress?.Report(channelLine);
            stdoutProgress?.Report(titleLine);
            stdoutProgress?.Report("download:PROGRESS=100%|SPEED=1MiB/s|ETA=00:00");
            return Task.FromResult(new YtDlpRunResult
            {
                ExitCode = 0,
                StandardOutput = $"{channelLine}\n{titleLine}",
            });
        }
    }
}
