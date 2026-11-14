// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpVideoCommentsServiceTests : IDisposable
{
    private const string WatchUrl = "https://www.youtube.com/watch?v=abc123";
    private readonly string _root;

    public YtDlpVideoCommentsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-comments-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Fact]
    public void BuildArguments_RequestsTopRootCommentsWithoutDownloading()
    {
        var args = YtDlpVideoCommentsService.BuildArguments(new AppConfiguration(), new JsRuntimeLocator(), WatchUrl);

        Assert.Equal(
            [
                "-J",
                "--skip-download",
                "--ignore-config",
                "--write-comments",
                "--extractor-args",
                "youtube:max_comments=100,all,0,0;comment_sort=top",
                "--",
                WatchUrl,
            ],
            args);
    }

    [Fact]
    public void BuildArguments_WithJsRuntime_AddsJsRuntimesBeforeUrl()
    {
        var config = new AppConfiguration { JsRuntimeEngine = "deno" };

        var args = YtDlpVideoCommentsService.BuildArguments(config, new FixedJsRuntimeLocator("/opt/deno"), WatchUrl);

        var index = args.ToList().IndexOf("--js-runtimes");
        Assert.True(index > 0);
        Assert.Equal("deno:/opt/deno", args[index + 1]);
        Assert.Equal(WatchUrl, args[^1]);
        Assert.Equal("--", args[^2]);
    }

    [Fact]
    public async Task GetTopCommentsAsync_PassesArgumentsAndParsesOutput()
    {
        const string json = """
            { "comments": [ { "id": "c1", "parent": "root", "author": "A", "text": "Hi" } ] }
            """;
        var process = new CapturingProcessRunner(0, json);
        var service = await CreateServiceAsync(process);

        var comments = await service.GetTopCommentsAsync(WatchUrl, TestContext.Current.CancellationToken);

        Assert.Equal("c1", Assert.Single(comments).Id);
        Assert.NotNull(process.Invocation);
        Assert.Equal(
            YtDlpVideoCommentsService.BuildArguments(new AppConfiguration(), new JsRuntimeLocator(), WatchUrl),
            process.Invocation.Arguments);
        Assert.Equal(Path.Combine(_root, "bin", "yt-dlp", "yt-dlp"), process.Invocation.ExecutablePath);
    }

    [Fact]
    public async Task GetTopCommentsAsync_NonZeroExit_ThrowsFailureMessage()
    {
        var failed = new YtDlpRunResult { ExitCode = 1, StandardError = "ERROR: Sign in to confirm" };
        var service = await CreateServiceAsync(new CapturingProcessRunner(failed));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTopCommentsAsync(WatchUrl, TestContext.Current.CancellationToken));

        Assert.Equal(YtDlpFailureMessageBuilder.Build(failed), ex.Message);
    }

    [Fact]
    public async Task GetTopCommentsAsync_NonZeroExitWithoutOutput_ThrowsFallbackMessage()
    {
        var failed = new YtDlpRunResult { ExitCode = 1, StandardError = "  " };
        var service = await CreateServiceAsync(new CapturingProcessRunner(failed));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTopCommentsAsync(WatchUrl, TestContext.Current.CancellationToken));

        Assert.Equal("yt-dlp exited with code 1.", ex.Message);
    }

    [Fact]
    public async Task GetTopCommentsAsync_MissingYtDlp_Throws()
    {
        var binaryLocator = Substitute.For<IBinaryLocator>();
        binaryLocator.ResolveYtDlpPath(Arg.Any<AppConfiguration>()).Returns((string?)null);
        var service = new YtDlpVideoCommentsService(
            new CapturingProcessRunner(0, "{}"),
            new AppConfigStore(_root, new ProfileStore(_root)),
            binaryLocator,
            new JsRuntimeLocator());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTopCommentsAsync(WatchUrl, TestContext.Current.CancellationToken));

        Assert.Equal("yt-dlp was not found. Install it or set its path in Settings.", ex.Message);
    }

    [Fact]
    public async Task GetTopCommentsAsync_BlankWatchUrl_Throws()
    {
        var service = await CreateServiceAsync(new CapturingProcessRunner(0, "{}"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetTopCommentsAsync("  ", TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task<YtDlpVideoCommentsService> CreateServiceAsync(IYtDlpProcessRunner process)
    {
        var appConfig = new AppConfigStore(_root, new ProfileStore(_root));
        await appConfig.EnsureBootstrapAsync(TestContext.Current.CancellationToken);
        var config = await appConfig.LoadAsync(TestContext.Current.CancellationToken);
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config, TestContext.Current.CancellationToken);

        return new YtDlpVideoCommentsService(process, appConfig, new BinaryLocator(_root), new JsRuntimeLocator());
    }

    private sealed class FixedJsRuntimeLocator(string path) : IJsRuntimeLocator
    {
        public string? ResolvePath(AppConfiguration config) => path;
    }

    private sealed class CapturingProcessRunner : IYtDlpProcessRunner
    {
        private readonly YtDlpRunResult _result;

        public CapturingProcessRunner(int exitCode, string stdout)
            : this(new YtDlpRunResult { ExitCode = exitCode, StandardOutput = stdout })
        {
        }

        public CapturingProcessRunner(YtDlpRunResult result) => _result = result;

        public YtDlpInvocation? Invocation { get; private set; }

        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default)
        {
            Invocation = invocation;
            return Task.FromResult(_result);
        }
    }
}
