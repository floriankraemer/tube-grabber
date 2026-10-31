// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void NormalizeUrlInput_StripsYouTubeQuery()
    {
        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });

        var vm = ViewModelTestHelpers.CreateMainViewModel(appConfig: appConfig);

        vm.UrlInput = "https://www.youtube.com/watch?v=abc&t=1";
        vm.NormalizeUrlInput();
        Assert.Equal("https://www.youtube.com/watch?v=abc", vm.UrlInput);
    }

    [Fact]
    public async Task AddUrlAsync_EnqueuesWithActiveProfile()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns(Array.Empty<DownloadJob>());
        var job = new DownloadJob { Url = "https://youtu.be/abc", ProfileId = "default" };
        queue.EnqueueAsync(Arg.Any<string>(), "default", Arg.Any<CancellationToken>()).Returns(job);

        var ytDlpPath = Path.Combine(Path.GetTempPath(), $"yt-dlp-test-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(ytDlpPath, string.Empty, TestContext.Current.CancellationToken);

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default", YtDlpPath = ytDlpPath, DownloadFolder = Path.GetTempPath() });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new DownloadProfile { Id = "default", Name = "Default" },
                new DownloadProfile { Id = "audio-mp3", Name = "Download Audio as mp3" },
            });
        profileStore.GetAsync("default", Arg.Any<CancellationToken>())
            .Returns(new DownloadProfile { Id = "default", Name = "Default" });

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig, profileStore);

        await vm.RefreshProfilesAsync();
        vm.UrlInput = "https://youtu.be/abc?si=x";
        await vm.AddUrlAsync();

        await queue.Received(1).EnqueueAsync("https://youtu.be/abc", "default", Arg.Any<CancellationToken>());
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task AddUrlAsync_EnqueuesWithSelectedProfile()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns(Array.Empty<DownloadJob>());
        var job = new DownloadJob { Url = "https://youtu.be/abc", ProfileId = "audio-mp3" };
        queue.EnqueueAsync(Arg.Any<string>(), "audio-mp3", Arg.Any<CancellationToken>()).Returns(job);

        var ytDlpPath = Path.Combine(Path.GetTempPath(), $"yt-dlp-test-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(ytDlpPath, string.Empty, TestContext.Current.CancellationToken);

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default", YtDlpPath = ytDlpPath, DownloadFolder = Path.GetTempPath() });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new DownloadProfile { Id = "default", Name = "Default" },
                new DownloadProfile { Id = "audio-mp3", Name = "Download Audio as mp3" },
            });
        profileStore.GetAsync("audio-mp3", Arg.Any<CancellationToken>())
            .Returns(new DownloadProfile { Id = "audio-mp3", Name = "Download Audio as mp3" });

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig, profileStore);

        await vm.RefreshProfilesAsync();
        vm.SelectedProfile = vm.Profiles.First(profile => profile.Id == "audio-mp3");
        vm.UrlInput = "https://youtu.be/abc";
        await vm.AddUrlAsync();

        await queue.Received(1).EnqueueAsync("https://youtu.be/abc", "audio-mp3", Arg.Any<CancellationToken>());
    }
}
