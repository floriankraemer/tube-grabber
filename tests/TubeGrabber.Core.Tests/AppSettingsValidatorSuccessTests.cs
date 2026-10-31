// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class AppSettingsValidatorSuccessTests
{
    [Fact]
    public void Validate_ValidConfiguration_ReturnsNoErrors()
    {
        var validator = new AppSettingsValidator(new ExtraArgsTokenizer(), new DownloadFolderService());
        var config = new AppConfiguration { MaxConcurrentDownloads = 2 };
        var profile = new DownloadProfile { Id = "p", Name = "Profile" };
        Assert.Empty(validator.Validate(config, profile));
    }
}
