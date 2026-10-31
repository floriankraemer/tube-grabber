// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class SettingsCoordinator
{
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly YtDlpCommandBuilder _commandBuilder;
    private readonly AppSettingsValidator _validator;
    private readonly IBinaryLocator _binaryLocator;
    private readonly IJsRuntimeLocator _jsRuntimeLocator;

    public SettingsCoordinator(
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        YtDlpCommandBuilder commandBuilder,
        AppSettingsValidator validator,
        IBinaryLocator binaryLocator,
        IJsRuntimeLocator jsRuntimeLocator)
    {
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _commandBuilder = commandBuilder;
        _validator = validator;
        _binaryLocator = binaryLocator;
        _jsRuntimeLocator = jsRuntimeLocator;
    }

    public async Task<(AppConfiguration Config, IReadOnlyList<DownloadProfile> Profiles)> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _appConfigStore.LoadAsync(cancellationToken);
        var profiles = await _profileStore.ListAsync(cancellationToken);
        return (config, profiles);
    }

    public IReadOnlyList<string> Validate(AppConfiguration config, DownloadProfile profile) =>
        _validator.Validate(config, profile);

    /// <summary>
    /// Writes the profiles and the settings. The active profile and the queue column widths belong to other pages, which
    /// persist them on their own, so the copy held by Settings must not overwrite them.
    /// </summary>
    public async Task SaveAsync(
        AppConfiguration config,
        IEnumerable<DownloadProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        foreach (var profile in profiles)
            await _profileStore.SaveAsync(profile, cancellationToken);

        var latest = await _appConfigStore.LoadAsync(cancellationToken);
        config.ActiveProfileId = latest.ActiveProfileId;
        config.QueueColumnWidths = latest.QueueColumnWidths;
        await _appConfigStore.SaveAsync(config, cancellationToken);
    }

    /// <summary>Deletes a custom profile. Built-in profiles are recreated on start-up, so deleting one is refused.</summary>
    public async Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        if (BuiltInProfiles.IsBuiltIn(profileId))
            throw new InvalidOperationException("Built-in profiles can't be deleted.");

        await _profileStore.DeleteAsync(profileId, cancellationToken);

        var config = await _appConfigStore.LoadAsync(cancellationToken);
        if (string.Equals(config.ActiveProfileId, profileId, StringComparison.Ordinal))
        {
            config.ActiveProfileId = Constants.AppPaths.DefaultProfileId;
            await _appConfigStore.SaveAsync(config, cancellationToken);
        }
    }

    public string BuildCliPreview(AppConfiguration config, DownloadProfile profile)
    {
        var jsRuntimesArgument = JsRuntimeArgumentBuilder.Build(config, _jsRuntimeLocator);
        return _commandBuilder.BuildPreview(
            profile,
            _binaryLocator.ResolveFfmpegPath(config),
            jsRuntimesArgument,
            "https://www.youtube.com/watch?v=example");
    }

    public async Task<string?> TestYtDlpAsync(AppConfiguration config, CancellationToken cancellationToken = default)
    {
        var path = ResolveYtDlpPath(config);
        if (path is null)
            return "yt-dlp executable not found. Set a path below or use Install.";

        var version = await YtDlpProcessRunner.GetVersionAsync(path, cancellationToken);
        return version ?? "yt-dlp test failed. Check the path and try again.";
    }

    public async Task<string?> TestFfmpegAsync(AppConfiguration config, CancellationToken cancellationToken = default)
    {
        var path = ResolveFfmpegPath(config);
        if (path is null)
            return "ffmpeg executable not found. Set a path below or use Install.";

        var version = await YtDlpProcessRunner.GetFirstOutputLineAsync(path, ["-version"], cancellationToken);
        return version ?? "ffmpeg test failed. Check the path and try again.";
    }

    public string? ResolveYtDlpPath(AppConfiguration config) =>
        NormalizeExistingPath(_binaryLocator.ResolveYtDlpPath(config) ?? config.YtDlpPath);

    public string? ResolveFfmpegPath(AppConfiguration config) =>
        NormalizeExistingPath(_binaryLocator.ResolveFfmpegPath(config) ?? config.FfmpegPath);

    public string? ResolveJsRuntimePath(AppConfiguration config) =>
        _jsRuntimeLocator.ResolvePath(config);

    public string? BuildJsRuntimesArgument(AppConfiguration config) =>
        JsRuntimeArgumentBuilder.Build(config, _jsRuntimeLocator);

    public async Task<string?> TestJsRuntimeAsync(AppConfiguration config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.JsRuntimeEngine)
            || !Constants.JsRuntimeEngines.IsSupported(config.JsRuntimeEngine))
            return "Select a JavaScript runtime engine (e.g. Deno) to test.";

        var engine = config.JsRuntimeEngine.Trim();
        var path = ResolveJsRuntimePath(config);
        if (path is null)
            return $"{engine} executable not found. Set a path or install it on PATH.";

        var version = await YtDlpProcessRunner.GetFirstOutputLineAsync(path, ["--version"], cancellationToken)
            ?? await YtDlpProcessRunner.GetFirstOutputLineAsync(path, ["-v"], cancellationToken);
        return version ?? $"{engine} test failed. Check the path and try again.";
    }

    private static string? NormalizeExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var fullPath = Path.GetFullPath(path.Trim());
        return File.Exists(fullPath) ? fullPath : null;
    }
}
