// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.IO.Compression;
using System.Net;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class DenoBinaryInstallerTests : IDisposable
{
    private readonly string _root;

    public DenoBinaryInstallerTests() =>
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InstallAsync_DownloadsAndExtractsDeno()
    {
        var handler = new FakeZipHttpMessageHandler();
        var helper = new BinaryDownloadHelper(new HttpClient(handler));
        var source = new GitHubBinaryReleaseSource();
        var locator = new BinaryLocator(_root);
        var installer = new DenoBinaryInstaller(source, locator, helper, "linux-x64");

        var result = await installer.InstallAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(result.InstalledPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeZipHttpMessageHandler : HttpMessageHandler
    {
        private const int MinimumArchiveSizeBytes = 1_000_100;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var denoEntry = archive.CreateEntry("deno", CompressionLevel.NoCompression);
                using (var stream = denoEntry.Open())
                {
                    var payload = new byte[100_000];
                    stream.Write(payload, 0, payload.Length);
                }

                var paddingEntry = archive.CreateEntry("padding.bin", CompressionLevel.NoCompression);
                using (var stream = paddingEntry.Open())
                {
                    var padding = new byte[MinimumArchiveSizeBytes];
                    stream.Write(padding, 0, padding.Length);
                }
            }

            memory.Position = 0;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(memory.ToArray()),
            });
        }
    }
}
