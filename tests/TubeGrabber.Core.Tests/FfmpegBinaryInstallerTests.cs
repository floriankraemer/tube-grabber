// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.IO.Compression;
using System.Net;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class FfmpegBinaryInstallerTests : IDisposable
{
    private const string WindowsRuntimeIdentifier = "win-x64";
    private const int ExecutableSizeBytes = 100_000;

    private readonly string _root;
    private readonly string _extractDirectory;

    public FfmpegBinaryInstallerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _extractDirectory = Path.Combine(_root, AppPaths.BinFolderName, "ffmpeg", "extract");
    }

    [Fact]
    public async Task InstallAsync_KeepsInstalledBinaryAndRemovesExtractDirectory()
    {
        var executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var installer = CreateInstaller(new FakeArchiveHttpMessageHandler($"ffmpeg-master-latest-win64-gpl/bin/{executableName}"));

        var result = await installer.InstallAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutableSizeBytes, new FileInfo(result.InstalledPath!).Length);
        Assert.False(Directory.Exists(_extractDirectory));
    }

    [Fact]
    public async Task InstallAsync_RemovesExtractDirectoryWhenExtractionFails()
    {
        var installer = CreateInstaller(new FakeArchiveHttpMessageHandler("readme.txt"));

        var result = await installer.InstallAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(Directory.Exists(_extractDirectory));
    }

    private FfmpegBinaryInstaller CreateInstaller(HttpMessageHandler handler) =>
        new(
            new GitHubBinaryReleaseSource(),
            new BinaryLocator(_root),
            new BinaryDownloadHelper(new HttpClient(handler)),
            WindowsRuntimeIdentifier);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeArchiveHttpMessageHandler(string entryName) : HttpMessageHandler
    {
        private const int MinimumArchiveSizeBytes = 1_000_100;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                using (var stream = entry.Open())
                {
                    var payload = new byte[ExecutableSizeBytes];
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
