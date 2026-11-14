// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpVideoDetailsService : IVideoDetailsService
{
    private const string FallbackErrorMessage =
        "Loading the video failed. Check that a JS runtime is configured in Settings.";

    private readonly IYtDlpProcessRunner _processRunner;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IBinaryLocator _binaryLocator;
    private readonly IJsRuntimeLocator _jsRuntimeLocator;
    private readonly YtDlpVideoDetailsParser _parser;

    public YtDlpVideoDetailsService(
        IYtDlpProcessRunner processRunner,
        IAppConfigStore appConfigStore,
        IBinaryLocator binaryLocator,
        IJsRuntimeLocator jsRuntimeLocator,
        YtDlpVideoDetailsParser? parser = null)
    {
        _processRunner = processRunner;
        _appConfigStore = appConfigStore;
        _binaryLocator = binaryLocator;
        _jsRuntimeLocator = jsRuntimeLocator;
        _parser = parser ?? new YtDlpVideoDetailsParser();
    }

    public async Task<VideoDetails> GetDetailsAsync(string watchUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(watchUrl))
            throw new ArgumentException("A video URL is required.", nameof(watchUrl));

        var config = await _appConfigStore.LoadAsync(cancellationToken);
        var ytDlpPath = _binaryLocator.ResolveYtDlpPath(config)
            ?? throw new InvalidOperationException("yt-dlp was not found. Install it or set its path in Settings.");

        var invocation = new YtDlpInvocation
        {
            ExecutablePath = ytDlpPath,
            Arguments = BuildArguments(config, _jsRuntimeLocator, watchUrl.Trim()),
        };

        var result = await _processRunner.RunAsync(invocation, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.ExitCode != 0)
        {
            var message = YtDlpFailureMessageBuilder.Build(result);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? FallbackErrorMessage : message);
        }

        return _parser.Parse(result.StandardOutput);
    }

    public static IReadOnlyList<string> BuildArguments(
        AppConfiguration config,
        IJsRuntimeLocator jsRuntimeLocator,
        string watchUrl)
    {
        var args = new List<string> { "-J", "--skip-download", "--ignore-config" };

        var jsRuntimes = JsRuntimeArgumentBuilder.Build(config, jsRuntimeLocator);
        if (!string.IsNullOrWhiteSpace(jsRuntimes))
        {
            args.Add("--js-runtimes");
            args.Add(jsRuntimes);
        }

        // "--" so a URL can never be parsed as an option.
        args.Add("--");
        args.Add(watchUrl);
        return args;
    }
}
