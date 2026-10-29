// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class ProfileStoreListTests
{
    [Fact]
    public async Task ListAsync_WhenDirectoryMissing_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));
        var store = new ProfileStore(root);
        var profiles = await store.ListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(profiles);
    }
}
