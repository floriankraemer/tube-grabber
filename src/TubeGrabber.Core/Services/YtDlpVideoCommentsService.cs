// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Globalization;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpVideoCommentsService : IVideoCommentsService
{
    public const int MaxComments = 100;

    private const string FailureMessage =
        "Could not load comments. Check that a JS runtime is configured in Settings.";

    private readonly IYtDlpProcessRunner _processRunner;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IBinaryLocator _binaryLocator;
    private readonly IJsRuntimeLocator _jsRuntimeLocator;
    private readonly YtDlpCommentsParser _parser;

    public YtDlpVideoCommentsService(
        IYtDlpProcessRunner processRunner,
        IAppConfigStore appConfigStore,
        IBinaryLocator binaryLocator,
        IJsRuntimeLocator jsRuntimeLocator,
        YtDlpCommentsParser? parser = null)
    {
        _processRunner = processRunner;
        _appConfigStore = appConfigStore;
        _binaryLocator = binaryLocator;
        _jsRuntimeLocator = jsRuntimeLocator;
        _parser = parser ?? new YtDlpCommentsParser();
    }

    public async Task<IReadOnlyList<VideoComment>> GetTopCommentsAsync(
        string watchUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchUrl);

        var config = await _appConfigStore.LoadAsync(cancellationToken);
        var ytDlpPath = _binaryLocator.ResolveYtDlpPath(config)
            ?? throw new InvalidOperationException("yt-dlp was not found. Install it or set its path in Settings.");

        var invocation = new YtDlpInvocation
        {
            ExecutablePath = ytDlpPath,
            Arguments = BuildArguments(config, _jsRuntimeLocator, watchUrl),
        };

        var result = await _processRunner.RunAsync(invocation, cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            var message = YtDlpFailureMessageBuilder.Build(result);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message) ? FailureMessage : message);
        }

        return _parser.Parse(result.StandardOutput);
    }

    public static IReadOnlyList<string> BuildArguments(
        AppConfiguration config,
        IJsRuntimeLocator jsRuntimeLocator,
        string watchUrl)
    {
        var args = new List<string>
        {
            "-J",
            "--skip-download",
            "--ignore-config",
            "--write-comments",
            "--extractor-args",
            $"youtube:max_comments={MaxComments.ToString(CultureInfo.InvariantCulture)},all,0,0;comment_sort=top",
        };

        var jsRuntimes = JsRuntimeArgumentBuilder.Build(config, jsRuntimeLocator);
        if (!string.IsNullOrWhiteSpace(jsRuntimes))
        {
            args.Add("--js-runtimes");
            args.Add(jsRuntimes);
        }

        args.Add(watchUrl);
        return args;
    }
}
