// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpVideoDetailsServiceTests : IDisposable
{
    private const string WatchUrl = "https://www.youtube.com/watch?v=abc";
    private readonly string _root;
    private readonly IYtDlpProcessRunner _runner = Substitute.For<IYtDlpProcessRunner>();

    public YtDlpVideoDetailsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-details-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Fact]
    public async Task GetDetailsAsync_RunsYtDlpWithExpectedArgumentsAndParsesOutput()
    {
        YtDlpInvocation? captured = null;
        _runner.RunAsync(Arg.Do<YtDlpInvocation>(i => captured = i), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new YtDlpRunResult { ExitCode = 0, StandardOutput = """{ "id": "abc", "title": "Hello" }""" });
        var service = await CreateServiceAsync();

        var details = await service.GetDetailsAsync(WatchUrl, TestContext.Current.CancellationToken);

        Assert.Equal("Hello", details.Title);
        Assert.NotNull(captured);
        Assert.Equal(["-J", "--skip-download", "--ignore-config"], captured.Arguments.Take(3));
        Assert.Equal(WatchUrl, captured.Arguments[^1]);
        Assert.Equal("--", captured.Arguments[^2]);
    }

    [Fact]
    public async Task GetDetailsAsync_NonZeroExit_SurfacesError()
    {
        _runner.RunAsync(Arg.Any<YtDlpInvocation>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new YtDlpRunResult { ExitCode = 1, StandardError = "ERROR: Video unavailable" });
        var service = await CreateServiceAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetDetailsAsync(WatchUrl, TestContext.Current.CancellationToken));

        Assert.Contains("Video unavailable", ex.Message);
    }

    [Fact]
    public async Task GetDetailsAsync_NonZeroExitWithoutOutput_UsesFallbackMessage()
    {
        _runner.RunAsync(Arg.Any<YtDlpInvocation>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new YtDlpRunResult { ExitCode = 1 });
        var service = await CreateServiceAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetDetailsAsync(WatchUrl, TestContext.Current.CancellationToken));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task GetDetailsAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        _runner.RunAsync(Arg.Any<YtDlpInvocation>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return new YtDlpRunResult { ExitCode = -1, WasCancelled = true };
            });
        var service = await CreateServiceAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetDetailsAsync(WatchUrl, cts.Token));
    }

    [Fact]
    public async Task GetDetailsAsync_BlankUrl_Throws()
    {
        var service = await CreateServiceAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetDetailsAsync(" ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetDetailsAsync_YtDlpMissing_Throws()
    {
        var service = new YtDlpVideoDetailsService(
            _runner,
            new AppConfigStore(_root, new ProfileStore(_root)),
            TestBinaryLocator.Create(Path.Combine(_root, "empty")),
            new JsRuntimeLocator());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetDetailsAsync(WatchUrl, TestContext.Current.CancellationToken));

        Assert.Contains("yt-dlp was not found", ex.Message);
    }

    private async Task<YtDlpVideoDetailsService> CreateServiceAsync()
    {
        var appConfig = new AppConfigStore(_root, new ProfileStore(_root));
        await appConfig.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        return new YtDlpVideoDetailsService(_runner, appConfig, TestBinaryLocator.Create(_root), new JsRuntimeLocator());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
