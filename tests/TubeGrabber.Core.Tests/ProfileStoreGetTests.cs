// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ProfileStoreGetTests
{
    [Fact]
    public async Task GetAsync_WhenMissing_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        var store = new ProfileStore(root);
        var profile = await store.GetAsync("missing", TestContext.Current.CancellationToken);
        Assert.Null(profile);
    }
}
