// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Net;
using System.Text;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpBinaryInstallerFailureTests : IDisposable
{
    private readonly string _root;

    public YtDlpBinaryInstallerFailureTests() =>
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InstallAsync_OnFailure_ReturnsManualDownloadUrl()
    {
        var handler = new FailingHttpMessageHandler();
        var helper = new BinaryDownloadHelper(new HttpClient(handler));
        var installer = new YtDlpBinaryInstaller(new GitHubBinaryReleaseSource(), new BinaryLocator(_root), helper, "linux-x64");

        var result = await installer.InstallAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(BinaryReleaseManifest.YtDlpReleasePageUrl, result.ManualDownloadUrl);
        Assert.Contains(BinaryReleaseManifest.YtDlpReleasePageUrl, result.Error, StringComparison.Ordinal);
        Assert.Contains("linux-x64", result.Error, StringComparison.Ordinal);
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
