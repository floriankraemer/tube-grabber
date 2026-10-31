// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class SubscriptionStore : ISubscriptionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SubscriptionStore(string configRoot) =>
        _filePath = Path.Combine(configRoot, AppPaths.SubscriptionsFileName);

    public async Task<IReadOnlyList<ChannelSubscription>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        SubscriptionsDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<SubscriptionsDocument>(stream, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Subscriptions file '{_filePath}' is corrupt and cannot be read. Fix or remove it to continue.", ex);
        }

        return document?.Subscriptions
            ?? throw new InvalidOperationException(
                $"Subscriptions file '{_filePath}' is empty or malformed. Fix or remove it to continue.");
    }

    public async Task SaveAsync(IReadOnlyList<ChannelSubscription> subscriptions, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        var document = new SubscriptionsDocument { Subscriptions = subscriptions.ToList() };
        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, ct);
        }

        File.Move(tempPath, _filePath, overwrite: true);
        ConfigFilePermissions.ApplyRestrictedPermissions(_filePath, isDirectory: false);
    }
}
