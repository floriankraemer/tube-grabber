// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class AppSettingsValidatorPathTests
{
    [Fact]
    public void Validate_InvalidBinaryPaths_ReturnsErrors()
    {
        var validator = new AppSettingsValidator(new ExtraArgsTokenizer(), new DownloadFolderService());
        var config = new AppConfiguration
        {
            YtDlpPath = "/does/not/exist/yt-dlp",
            FfmpegPath = "/does/not/exist/ffmpeg",
        };
        var profile = new DownloadProfile { Id = "p", Name = "Profile" };
        var errors = validator.Validate(config, profile);
        Assert.Equal(2, errors.Count);
    }
}
