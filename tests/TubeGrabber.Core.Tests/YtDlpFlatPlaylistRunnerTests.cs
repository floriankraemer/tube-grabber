// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class YtDlpFlatPlaylistRunnerTests : IDisposable
{
    private readonly string _root;

    public YtDlpFlatPlaylistRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-flat-playlist-tests", Guid.NewGuid().ToString("N"));
        var ytDlpDir = Path.Combine(_root, "bin", "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);
        File.WriteAllText(Path.Combine(ytDlpDir, "yt-dlp"), string.Empty);
    }

    [Fact]
    public void BuildArguments_UsesRangeAndTarget()
    {
        var args = YtDlpFlatPlaylistRunner.BuildArguments(
            new AppConfiguration(),
            new JsRuntimeLocator(),
            "https://www.youtube.com/@chan/videos",
            playlistStart: 1,
            playlistEnd: 5);

        Assert.Equal("--flat-playlist", args[0]);
        Assert.Contains("--dump-single-json", args);
        Assert.Contains("--skip-download", args);
        Assert.Contains("--ignore-config", args);
        Assert.Equal("1", args[args.ToList().IndexOf("--playlist-start") + 1]);
        Assert.Equal("5", args[args.ToList().IndexOf("--playlist-end") + 1]);
        Assert.Equal("https://www.youtube.com/@chan/videos", args[^1]);
    }

    [Fact]
    public async Task RunAsync_ReturnsStdout()
    {
        var runner = await CreateRunnerAsync(new FakeProcessRunner(0, "out", "err"));

        Assert.Equal("out", await runner.RunAsync("target", 1, 5, "fallback", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_BlankStdout_ReturnsStderr()
    {
        var runner = await CreateRunnerAsync(new FakeProcessRunner(0, "  ", "err"));

        Assert.Equal("err", await runner.RunAsync("target", 1, 5, "fallback", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ThrowsFailureMessage()
    {
        var failed = new YtDlpRunResult { ExitCode = 1, StandardError = "ERROR: boom" };
        var runner = await CreateRunnerAsync(new FakeProcessRunner(failed));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("target", 1, 5, "fallback", TestContext.Current.CancellationToken));

        Assert.Equal(YtDlpFailureMessageBuilder.Build(failed), ex.Message);
    }

    [Fact]
    public async Task RunAsync_MissingYtDlp_Throws()
    {
        var binaryLocator = Substitute.For<IBinaryLocator>();
        binaryLocator.ResolveYtDlpPath(Arg.Any<AppConfiguration>()).Returns((string?)null);
        var runner = new YtDlpFlatPlaylistRunner(
            new FakeProcessRunner(0, "out", string.Empty),
            new AppConfigStore(_root, new ProfileStore(_root)),
            binaryLocator,
            new JsRuntimeLocator());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("target", 1, 5, "fallback", TestContext.Current.CancellationToken));

        Assert.Equal("yt-dlp was not found. Install it or set its path in Settings.", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task<YtDlpFlatPlaylistRunner> CreateRunnerAsync(IYtDlpProcessRunner processRunner)
    {
        var appConfig = new AppConfigStore(_root, new ProfileStore(_root));
        await appConfig.EnsureBootstrapAsync();
        var config = await appConfig.LoadAsync();
        config.YtDlpPath = Path.Combine(_root, "bin", "yt-dlp", "yt-dlp");
        await appConfig.SaveAsync(config);

        return new YtDlpFlatPlaylistRunner(processRunner, appConfig, new BinaryLocator(_root), new JsRuntimeLocator());
    }

    private sealed class FakeProcessRunner(YtDlpRunResult result) : IYtDlpProcessRunner
    {
        public FakeProcessRunner(int exitCode, string stdout, string stderr)
            : this(new YtDlpRunResult { ExitCode = exitCode, StandardOutput = stdout, StandardError = stderr })
        {
        }

        public Task<YtDlpRunResult> RunAsync(
            YtDlpInvocation invocation,
            IProgress<string>? stdoutProgress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
