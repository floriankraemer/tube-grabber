// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class BinaryInstallServiceFfmpegTests
{
    [Fact]
    public async Task InstallAsync_UpdatesFfmpegPath()
    {
        var appConfig = Substitute.For<IAppConfigStore>();
        var config = new AppConfiguration();
        appConfig.LoadAsync(Arg.Any<CancellationToken>()).Returns(config);

        var installer = Substitute.For<IBinaryInstaller>();
        installer.InstallAsync(Arg.Any<CancellationToken>())
            .Returns(BinaryInstallResult.Success("/tmp/ffmpeg"));

        var service = new BinaryInstallService(appConfig, new BinaryLocator(Path.GetTempPath()));
        await service.InstallAsync(ManagedBinary.Ffmpeg, installer, TestContext.Current.CancellationToken);
        Assert.Equal("/tmp/ffmpeg", config.FfmpegPath);
    }
}
