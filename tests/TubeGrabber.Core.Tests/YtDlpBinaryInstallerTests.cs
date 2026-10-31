// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Net;
using System.Text;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpBinaryInstallerTests : IDisposable
{
    private readonly string _root;

    public YtDlpBinaryInstallerTests() =>
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InstallAsync_DownloadsBinary()
    {
        var handler = new FakeHttpMessageHandler();
        var helper = new BinaryDownloadHelper(new HttpClient(handler));
        var source = new GitHubBinaryReleaseSource();
        var locator = new BinaryLocator(_root);
        var installer = new YtDlpBinaryInstaller(source, locator, helper, "linux-x64");

        var result = await installer.InstallAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(result.InstalledPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetBytes(new string('x', 100_000));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
            return Task.FromResult(response);
        }
    }
}
