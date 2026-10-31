// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.Services;

public static class ProfileListLoader
{
    public static async Task<IReadOnlyList<DownloadProfile>> LoadOrderedAsync(
        IProfileStore profileStore,
        CancellationToken cancellationToken = default) =>
        (await profileStore.ListAsync(cancellationToken) ?? Array.Empty<DownloadProfile>())
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static async Task<(IReadOnlyList<DownloadProfile> Profiles, DownloadProfile? Selected)> LoadWithSelectionAsync(
        IProfileStore profileStore,
        IAppConfigStore appConfigStore,
        CancellationToken cancellationToken = default)
    {
        var profiles = await LoadOrderedAsync(profileStore, cancellationToken);
        var config = await appConfigStore.LoadAsync(cancellationToken);
        var selected = profiles.FirstOrDefault(profile => profile.Id == config.ActiveProfileId)
            ?? profiles.FirstOrDefault();
        return (profiles, selected);
    }
}
