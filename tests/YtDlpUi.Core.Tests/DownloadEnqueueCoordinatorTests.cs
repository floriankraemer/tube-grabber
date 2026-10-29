// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class DownloadEnqueueCoordinatorTests
{
    [Fact]
    public async Task TryEnqueueAsync_InvalidUrl_ReturnsFailure()
    {
        var coordinator = CreateCoordinator(Substitute.For<IDownloadQueueService>());

        var result = await coordinator.TryEnqueueAsync("not a url", "default", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task TryEnqueueAsync_MissingYtDlp_ReturnsFailure()
    {
        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppConfiguration());

        var coordinator = CreateCoordinator(
            Substitute.For<IDownloadQueueService>(),
            appConfig,
            new BinaryLocator(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        var result = await coordinator.TryEnqueueAsync("https://www.youtube.com/watch?v=abc", "default", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("yt-dlp", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryEnqueueAsync_Success_EnqueuesNormalizedUrl()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        var job = new DownloadJob { Url = "https://www.youtube.com/watch?v=abc", ProfileId = "default" };
        queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(job);

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default", DownloadFolder = "/tmp/downloads" });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.GetAsync("default", Arg.Any<CancellationToken>())
            .Returns(new DownloadProfile { Id = "default", Name = "Default" });

        var root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-coord-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);

        var coordinator = CreateCoordinator(queue, appConfig, new BinaryLocator(root), profileStore);

        var result = await coordinator.TryEnqueueAsync(
            "https://www.youtube.com/watch?v=abc&t=5",
            "default", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        await queue.Received(1).EnqueueAsync(
            "https://www.youtube.com/watch?v=abc",
            "default",
            Arg.Any<CancellationToken>());
    }

    private static DownloadEnqueueCoordinator CreateCoordinator(
        IDownloadQueueService queue,
        IAppConfigStore? appConfig = null,
        BinaryLocator? binaryLocator = null,
        IProfileStore? profileStore = null) =>
        new(
            queue,
            appConfig ?? Substitute.For<IAppConfigStore>(),
            profileStore ?? Substitute.For<IProfileStore>(),
            new YouTubeUrlNormalizer(),
            binaryLocator ?? new BinaryLocator(Path.GetTempPath()),
            new DownloadFolderService());
}
