// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class DownloadJobViewModelRefreshTests
{
    [Fact]
    public void Refresh_NotifiesAllDisplayProperties()
    {
        var job = new DownloadJob
        {
            Url = "https://example.com",
            ProfileId = "default",
            Title = "Title",
            Status = DownloadStatus.Running,
            Progress = 10,
            Speed = "1MiB/s",
            Eta = "00:10",
            Error = "err",
        };

        var vm = new DownloadJobViewModel(job);
        job.Status = DownloadStatus.Completed;
        job.Progress = 100;
        vm.Refresh();

        Assert.Equal(DownloadStatus.Completed, vm.Status);
        Assert.Equal(100, vm.Progress);
        Assert.False(vm.CanCancel);
        Assert.True(vm.CanRemove);
        Assert.False(vm.CanOpenOutput);
    }

    [Fact]
    public void StatusText_ShowsProcessingDuringPostProcess()
    {
        var job = new DownloadJob
        {
            Url = "https://example.com",
            ProfileId = "audio-mp3",
            Status = DownloadStatus.Running,
            ProgressPhase = DownloadProgressPhase.PostProcessing,
            ProgressActivity = "Converting audio",
            UseIndeterminateProgress = true,
            Progress = 100,
        };

        var vm = new DownloadJobViewModel(job);
        Assert.Equal("Processing", vm.StatusText);
        Assert.True(vm.IsProgressIndeterminate);
        Assert.Equal("Converting audio", vm.ProgressDisplayText);
        Assert.Equal("Converting audio", vm.Speed);
    }
}
