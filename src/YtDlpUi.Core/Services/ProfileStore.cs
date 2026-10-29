// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class ProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _profilesDirectory;

    public ProfileStore(string configRoot) =>
        _profilesDirectory = Path.Combine(configRoot, AppPaths.ProfilesFolderName);

    public async Task<IReadOnlyList<DownloadProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_profilesDirectory))
            return [];

        var profiles = new List<DownloadProfile>();
        foreach (var file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            var profile = await LoadFileAsync(file, cancellationToken);
            if (profile is not null)
                profiles.Add(profile);
        }

        return profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<DownloadProfile?> GetAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var path = GetProfilePath(profileId);
        return File.Exists(path) ? await LoadFileAsync(path, cancellationToken) : null;
    }

    public async Task SaveAsync(DownloadProfile profile, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_profilesDirectory);
        SettingsMigration.MigrateProfile(profile);

        // Write to a unique temp file and move it into place, so concurrent readers never hit a locked
        // or half-written profile.
        var path = GetProfilePath(profile.Id);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken);
            }

            ConfigFilePermissions.ApplyRestrictedPermissions(tempPath, isDirectory: false);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    public async Task DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var profiles = await ListAsync(cancellationToken);
        if (profiles.Count <= 1)
            throw new InvalidOperationException("Cannot delete the last profile.");

        var path = GetProfilePath(profileId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public async Task<DownloadProfile> DuplicateAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var source = await GetAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");

        var duplicate = new DownloadProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{source.Name} (copy)",
            Options = new Dictionary<string, object?>(source.Options, StringComparer.Ordinal),
            ExtraArgs = source.ExtraArgs,
        };

        await SaveAsync(duplicate, cancellationToken);
        return duplicate;
    }

    private string GetProfilePath(string profileId) =>
        Path.Combine(_profilesDirectory, $"{profileId}.json");

    private static async Task<DownloadProfile?> LoadFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<DownloadProfile>(stream, JsonOptions, cancellationToken);
        return profile is null ? null : SettingsMigration.MigrateProfile(profile);
    }
}
