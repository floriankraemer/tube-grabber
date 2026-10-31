// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class AppSettingsValidatorTests
{
    private readonly AppSettingsValidator _validator = new(new ExtraArgsTokenizer(), new DownloadFolderService());

    [Fact]
    public void Validate_RejectsInvalidConcurrency()
    {
        var config = new AppConfiguration { MaxConcurrentDownloads = 99 };
        var profile = new DownloadProfile { Id = "p", Name = "P" };
        var errors = _validator.Validate(config, profile);
        Assert.Contains(errors, e => e.Contains("Max concurrent"));
    }

    [Theory]
    [InlineData(AppPaths.MinSubscriptionCheckIntervalMinutes - 1)]
    [InlineData(AppPaths.MaxSubscriptionCheckIntervalMinutes + 1)]
    public void Validate_RejectsSubscriptionCheckIntervalOutOfRange(int minutes)
    {
        var config = new AppConfiguration { SubscriptionCheckIntervalMinutes = minutes };
        var profile = new DownloadProfile { Id = "p", Name = "P" };
        var errors = _validator.Validate(config, profile);
        Assert.Contains(errors, e => e == "Subscription check interval must be between 15 and 1440 minutes.");
    }

    [Theory]
    [InlineData(AppPaths.MinSubscriptionCheckIntervalMinutes)]
    [InlineData(AppPaths.MaxSubscriptionCheckIntervalMinutes)]
    public void Validate_AcceptsSubscriptionCheckIntervalBoundaries(int minutes)
    {
        var config = new AppConfiguration { SubscriptionCheckIntervalMinutes = minutes };
        var profile = new DownloadProfile { Id = "p", Name = "P" };
        Assert.DoesNotContain(_validator.Validate(config, profile), e => e.Contains("Subscription check interval"));
    }

    [Fact]
    public void Validate_RejectsUnbalancedExtraArgs()
    {
        var config = new AppConfiguration();
        var profile = new DownloadProfile { Id = "p", Name = "P", ExtraArgs = "\"bad" };
        var errors = _validator.Validate(config, profile);
        Assert.NotEmpty(errors);
    }
}
