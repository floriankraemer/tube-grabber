// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.UI.ViewModels;

namespace YtDlpUi.UI.Tests;

public sealed class MainWindowViewModelErrorTests
{
    [Fact]
    public async Task AddUrlAsync_SetsErrorWhenYtDlpMissing()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns(Array.Empty<DownloadJob>());

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig);

        vm.UrlInput = "https://www.youtube.com/watch?v=abc";
        await vm.AddUrlAsync();

        Assert.Contains("yt-dlp", vm.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        await queue.DidNotReceive().EnqueueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddUrlAsync_SetsErrorOnQueueFailure()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns(Array.Empty<DownloadJob>());
        queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<DownloadJob>>(_ => throw new InvalidOperationException("queue failed"));

        var ytDlpPath = Path.Combine(Path.GetTempPath(), $"yt-dlp-test-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(ytDlpPath, string.Empty, TestContext.Current.CancellationToken);

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default", YtDlpPath = ytDlpPath, DownloadFolder = Path.GetTempPath() });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.GetAsync("default", Arg.Any<CancellationToken>())
            .Returns(new DownloadProfile { Id = "default", Name = "Default" });

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig, profileStore);

        vm.UrlInput = "https://www.youtube.com/watch?v=abc";
        await vm.AddUrlAsync();
        Assert.Equal("queue failed", vm.ErrorMessage);
    }

    [Fact]
    public async Task InitializeAsync_LoadsProfiles()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns(Array.Empty<DownloadJob>());

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.EnsureBootstrapAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        appConfig.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppConfiguration { ActiveProfileId = "default" });

        var profileStore = Substitute.For<IProfileStore>();
        profileStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new DownloadProfile { Id = "default", Name = "Default" }]);

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig, profileStore);

        await vm.InitializeAsync();
        Assert.Single(vm.Profiles);
    }
}
