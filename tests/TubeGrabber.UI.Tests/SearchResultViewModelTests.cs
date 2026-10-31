// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class SearchResultViewModelTests
{
    [Fact]
    public void Constructor_WithDuration_SetsDurationDisplay()
    {
        var profile = new DownloadProfile { Id = "default", Name = "Default" };
        var vm = new SearchResultViewModel(
            new YouTubeSearchResult
            {
                VideoId = "v1",
                Title = "Title",
                WatchUrl = "https://www.youtube.com/watch?v=v1",
                DurationSeconds = 3735,
            },
            [profile],
            profile,
            CreateCoordinator(success: true),
            Substitute.For<IThumbnailLoader>());

        Assert.Equal("1:02:15", vm.DurationDisplay);
    }

    [Fact]
    public async Task AddToQueueAsync_Success_SetsStatusMessage()
    {
        var profile = new DownloadProfile { Id = "default", Name = "Default" };
        var coordinator = CreateCoordinator(success: true);

        var vm = new SearchResultViewModel(
            new YouTubeSearchResult
            {
                VideoId = "v1",
                Title = "Title",
                WatchUrl = "https://www.youtube.com/watch?v=v1",
            },
            [profile],
            profile,
            coordinator,
            Substitute.For<IThumbnailLoader>());

        await vm.AddToQueueAsync();

        Assert.Equal("Added to queue.", vm.StatusMessage);
    }

    [Fact]
    public async Task AddToQueueAsync_Failure_SetsErrorStatus()
    {
        var profile = new DownloadProfile { Id = "default", Name = "Default" };
        var coordinator = CreateCoordinator(success: false);

        var vm = new SearchResultViewModel(
            new YouTubeSearchResult
            {
                VideoId = "v1",
                Title = "Title",
                WatchUrl = "https://www.youtube.com/watch?v=v1",
            },
            [profile],
            profile,
            coordinator,
            Substitute.For<IThumbnailLoader>());

        await vm.AddToQueueAsync();

        Assert.Contains("folder", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static DownloadEnqueueCoordinator CreateCoordinator(bool success)
    {
        var queue = Substitute.For<IDownloadQueueService>();
        if (success)
        {
            queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DownloadJob
                {
                    Url = "https://www.youtube.com/watch?v=v1",
                    ProfileId = "default",
                });
        }

        var appConfig = Substitute.For<IAppConfigStore>();
        if (success)
        {
            appConfig.LoadAsync(Arg.Any<CancellationToken>())
                .Returns(new AppConfiguration { ActiveProfileId = "default", DownloadFolder = "/tmp" });
        }
        else
        {
            appConfig.LoadAsync(Arg.Any<CancellationToken>())
                .Returns(new AppConfiguration { ActiveProfileId = "default" });
        }

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.GetAsync("default", Arg.Any<CancellationToken>())
            .Returns(new DownloadProfile { Id = "default", Name = "Default" });

        var root = Path.Combine(Path.GetTempPath(), "tube-grabber-search-vm", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);

        return new DownloadEnqueueCoordinator(
            queue,
            appConfig,
            profileStore,
            new YouTubeUrlNormalizer(),
            new BinaryLocator(root),
            new DownloadFolderService());
    }
}
