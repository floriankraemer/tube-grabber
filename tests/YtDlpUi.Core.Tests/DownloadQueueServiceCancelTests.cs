// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class DownloadQueueServiceCancelTests : IDisposable
{
    private readonly string _root;
    private readonly SlowRunner _runner = new();
    private readonly DownloadQueueService _queue;

    public DownloadQueueServiceCancelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        var profiles = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, profiles);
        var catalog = new YtDlpOptionCatalog();

        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);

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
    }

    [Fact]
    public async Task CancelAsync_MarksJobCancelled()
    {
        await new AppConfigStore(_root, new ProfileStore(_root)).EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await new AppConfigStore(_root, new ProfileStore(_root)).LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        config.DownloadFolder = _root;
        await new AppConfigStore(_root, new ProfileStore(_root)).SaveAsync(config, TestContext.Current.CancellationToken);

        var job = await _queue.EnqueueAsync("https://www.youtube.com/watch?v=abc", config.ActiveProfileId, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await _queue.CancelAsync(job.Id, TestContext.Current.CancellationToken);

        for (var i = 0; i < 50; i++)
        {
            var current = _queue.Jobs.Single(j => j.Id == job.Id);
            if (current.Status == DownloadStatus.Cancelled)
            {
                Assert.Contains("partial", current.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                return;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.Fail("Job was not cancelled.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class SlowRunner : IYtDlpProcessRunner
    {
        public async Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new YtDlpRunResult { ExitCode = -1, WasCancelled = true };
            }

            return new YtDlpRunResult { ExitCode = 0 };
        }
    }
}
