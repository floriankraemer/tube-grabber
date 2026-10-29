// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Net;
using System.Text;
using YtDlpUi.Core.Services;

namespace YtDlpUi.Core.Tests;

public sealed class YtDlpBinaryInstallerFallbackTests : IDisposable
{
    private readonly string _root;

    public YtDlpBinaryInstallerFallbackTests() =>
        _root = Path.Combine(Path.GetTempPath(), "yt-dlp-ui-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InstallAsync_OnFailure_ReturnsExistingBinary()
    {
        var locator = new BinaryLocator(_root);
        var existing = locator.GetBundledYtDlpPath();
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "existing", TestContext.Current.CancellationToken);

        var handler = new FailingHttpMessageHandler();
        var helper = new BinaryDownloadHelper(new HttpClient(handler));
        var installer = new YtDlpBinaryInstaller(new GitHubBinaryReleaseSource(), locator, helper, "linux-x64");

        var result = await installer.InstallAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(existing, result.InstalledPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
