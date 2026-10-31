// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class MainWindowViewModelQueueTests
{
    [Fact]
    public async Task CancelJobAsync_DelegatesToQueue()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        var job = new DownloadJob { Url = "https://example.com", ProfileId = "default", Status = DownloadStatus.Running };
        queue.Jobs.Returns([job]);

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue);

        await vm.CancelJobAsync(new DownloadJobViewModel(job));
        await queue.Received(1).CancelAsync(job.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncJobs_ReusesExistingViewModels()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        var job = new DownloadJob { Url = "https://example.com", ProfileId = "default" };
        queue.Jobs.Returns([job]);

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.EnsureBootstrapAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppConfiguration());

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig);

        await vm.InitializeAsync();
        var first = vm.Jobs[0];
        queue.Jobs.Returns([job]);
        vm.GetType().GetMethod("SyncJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(vm, null);
        Assert.Same(first, vm.Jobs[0]);
    }

    [Fact]
    public async Task FailedJob_DoesNotSurfaceItsErrorInTheStatusBar()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns([new DownloadJob { Url = "https://example.com/1", ProfileId = "default", Status = DownloadStatus.Failed, Error = "Video unavailable" }]);
        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.EnsureBootstrapAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppConfiguration());

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig);
        await vm.InitializeAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.Equal("Video unavailable", vm.Jobs[0].Error);
    }

    [Fact]
    public void ChangingPage_ClearsTransientError()
    {
        var vm = ViewModelTestHelpers.CreateMainViewModel(Substitute.For<IDownloadQueueService>(), Substitute.For<IAppConfigStore>());
        vm.SetErrorMessage("Could not find the downloaded file.");

        vm.CurrentPage = AppPage.Search;

        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task ActiveJobCount_CountsQueuedAndRunningOnly()
    {
        var queue = Substitute.For<IDownloadQueueService>();
        queue.Jobs.Returns(
        [
            new DownloadJob { Url = "https://example.com/1", ProfileId = "default", Status = DownloadStatus.Running },
            new DownloadJob { Url = "https://example.com/2", ProfileId = "default", Status = DownloadStatus.Queued },
            new DownloadJob { Url = "https://example.com/3", ProfileId = "default", Status = DownloadStatus.Completed },
            new DownloadJob { Url = "https://example.com/4", ProfileId = "default", Status = DownloadStatus.Failed },
        ]);

        var appConfig = Substitute.For<IAppConfigStore>();
        appConfig.EnsureBootstrapAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppConfiguration());

        var vm = ViewModelTestHelpers.CreateMainViewModel(queue, appConfig);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await vm.InitializeAsync();

        Assert.Equal(2, vm.ActiveJobCount);
        Assert.True(vm.HasActiveJobs);
        Assert.Contains(nameof(MainWindowViewModel.ActiveJobCount), changed);
    }

    [Fact]
    public void ActiveJobCount_IsZeroWithoutJobs()
    {
        var vm = ViewModelTestHelpers.CreateMainViewModel();

        Assert.Equal(0, vm.ActiveJobCount);
        Assert.False(vm.HasActiveJobs);
    }

    [Fact]
    public void CurrentPage_DefaultsToDownloads()
    {
        var vm = ViewModelTestHelpers.CreateMainViewModel();

        Assert.Equal(AppPage.Downloads, vm.CurrentPage);
        Assert.True(vm.IsDownloadsPage);
        Assert.True(vm.ShowStatusBar);
    }

    [Theory]
    [InlineData(AppPage.Search, nameof(MainWindowViewModel.IsSearchPage))]
    [InlineData(AppPage.Subscriptions, nameof(MainWindowViewModel.IsSubscriptionsPage))]
    [InlineData(AppPage.Settings, nameof(MainWindowViewModel.IsSettingsPage))]
    public void CurrentPage_SwitchesExactlyOnePageFlag(AppPage page, string expectedFlag)
    {
        var vm = ViewModelTestHelpers.CreateMainViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.CurrentPage = page;

        var flags = new Dictionary<string, bool>
        {
            [nameof(MainWindowViewModel.IsDownloadsPage)] = vm.IsDownloadsPage,
            [nameof(MainWindowViewModel.IsSearchPage)] = vm.IsSearchPage,
            [nameof(MainWindowViewModel.IsSubscriptionsPage)] = vm.IsSubscriptionsPage,
            [nameof(MainWindowViewModel.IsSettingsPage)] = vm.IsSettingsPage,
        };
        Assert.Equal([expectedFlag], flags.Where(f => f.Value).Select(f => f.Key));
        Assert.Contains(expectedFlag, changed);
        Assert.Equal(page != AppPage.Settings, vm.ShowStatusBar);
    }
}
