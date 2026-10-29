// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Globalization;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class YtDlpFlatPlaylistRunner
{
    private readonly IYtDlpProcessRunner _processRunner;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IBinaryLocator _binaryLocator;
    private readonly IJsRuntimeLocator _jsRuntimeLocator;

    public YtDlpFlatPlaylistRunner(
        IYtDlpProcessRunner processRunner,
        IAppConfigStore appConfigStore,
        IBinaryLocator binaryLocator,
        IJsRuntimeLocator jsRuntimeLocator)
    {
        _processRunner = processRunner;
        _appConfigStore = appConfigStore;
        _binaryLocator = binaryLocator;
        _jsRuntimeLocator = jsRuntimeLocator;
    }

    public async Task<string> RunAsync(
        string target,
        int playlistStart,
        int playlistEnd,
        string fallbackErrorMessage,
        CancellationToken cancellationToken = default)
    {
        var config = await _appConfigStore.LoadAsync(cancellationToken);
        var ytDlpPath = _binaryLocator.ResolveYtDlpPath(config)
            ?? throw new InvalidOperationException("yt-dlp was not found. Install it or set its path in Settings.");

        var invocation = new YtDlpInvocation
        {
            ExecutablePath = ytDlpPath,
            Arguments = BuildArguments(config, _jsRuntimeLocator, target, playlistStart, playlistEnd),
        };

        var result = await _processRunner.RunAsync(invocation, cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            var message = YtDlpFailureMessageBuilder.Build(result);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message) ? fallbackErrorMessage : message);
        }

        return string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;
    }

    public static IReadOnlyList<string> BuildArguments(
        AppConfiguration config,
        IJsRuntimeLocator jsRuntimeLocator,
        string target,
        int playlistStart,
        int playlistEnd)
    {
        var args = new List<string>
        {
            "--flat-playlist",
            "--dump-single-json",
            "--skip-download",
            "--ignore-config",
            "--playlist-start",
            playlistStart.ToString(CultureInfo.InvariantCulture),
            "--playlist-end",
            playlistEnd.ToString(CultureInfo.InvariantCulture),
        };

        var jsRuntimes = JsRuntimeArgumentBuilder.Build(config, jsRuntimeLocator);
        if (!string.IsNullOrWhiteSpace(jsRuntimes))
        {
            args.Add("--js-runtimes");
            args.Add(jsRuntimes);
        }

        args.Add(target);
        return args;
    }
}
