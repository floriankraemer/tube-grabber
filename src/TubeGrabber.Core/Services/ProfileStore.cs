// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

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

    private string GetProfilePath(string profileId) =>
        Path.Combine(_profilesDirectory, $"{profileId}.json");

    private static async Task<DownloadProfile?> LoadFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<DownloadProfile>(stream, JsonOptions, cancellationToken);
        return profile is null ? null : SettingsMigration.MigrateProfile(profile);
    }
}
