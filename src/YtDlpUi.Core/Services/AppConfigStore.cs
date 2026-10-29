// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using System.Text.Json.Serialization;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class AppConfigStore : IAppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _configFilePath;
    private readonly IProfileStore _profileStore;

    // LoadAsync bootstraps built-in profiles and may write files; concurrent callers (UI, queue, subscription monitor)
    // must not interleave those writes.
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public AppConfigStore(string configRoot, IProfileStore profileStore)
    {
        ConfigRoot = configRoot;
        _configFilePath = Path.Combine(configRoot, AppPaths.AppConfigFileName);
        _profileStore = profileStore;
    }

    public string ConfigRoot { get; }

    public static AppConfigStore CreateDefault(IProfileStore profileStore)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        return new AppConfigStore(Path.Combine(home, AppPaths.ConfigFolderName), profileStore);
    }

    public async Task EnsureBootstrapAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBootstrapLockedAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task EnsureBootstrapLockedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ConfigRoot);
        ConfigFilePermissions.ApplyRestrictedPermissions(ConfigRoot, isDirectory: true);

        var profilesDir = Path.Combine(ConfigRoot, AppPaths.ProfilesFolderName);
        Directory.CreateDirectory(profilesDir);
        ConfigFilePermissions.ApplyRestrictedPermissions(profilesDir, isDirectory: true);

        await EnsureBuiltInProfilesAsync(cancellationToken);

        if (!File.Exists(_configFilePath))
            await SaveLockedAsync(new AppConfiguration(), cancellationToken);
    }

    private async Task EnsureBuiltInProfilesAsync(CancellationToken cancellationToken)
    {
        foreach (var template in BuiltInProfiles.All)
        {
            var existing = await _profileStore.GetAsync(template.Id, cancellationToken);
            if (existing is null)
            {
                await _profileStore.SaveAsync(template, cancellationToken);
                continue;
            }

            if (BuiltInProfileSynchronizer.MergeMissingFromTemplate(existing, template))
                await _profileStore.SaveAsync(existing, cancellationToken);
        }
    }

    public async Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            return await LoadLockedAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<AppConfiguration> LoadLockedAsync(CancellationToken cancellationToken)
    {
        await EnsureBootstrapLockedAsync(cancellationToken);

        if (!File.Exists(_configFilePath))
            return new AppConfiguration();

        await using var stream = File.OpenRead(_configFilePath);
        var config = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, JsonOptions, cancellationToken)
            ?? new AppConfiguration();

        return SettingsMigration.MigrateAppConfig(config);
    }

    public async Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            await SaveLockedAsync(configuration, cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task SaveLockedAsync(AppConfiguration configuration, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ConfigRoot);
        SettingsMigration.MigrateAppConfig(configuration);

        await using var stream = File.Create(_configFilePath);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken);
        ConfigFilePermissions.ApplyRestrictedPermissions(_configFilePath, isDirectory: false);
    }

}
