// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class BuiltInProfileSynchronizer
{
    private static readonly IReadOnlyDictionary<string, string[]> IncompleteSignatures = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [AppPaths.AudioMp3ProfileId] = ["-x", "--audio-format", "--force-overwrites"],
    };

    public static bool MergeMissingFromTemplate(DownloadProfile existing, DownloadProfile template)
    {
        var changed = false;

        foreach (var (key, value) in template.Options)
        {
            if (!existing.Options.ContainsKey(key))
            {
                existing.Options[key] = value;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(existing.ExtraArgs)
            && !string.IsNullOrWhiteSpace(template.ExtraArgs))
        {
            existing.ExtraArgs = template.ExtraArgs;
            changed = true;
        }

        if (IsIncompleteBuiltIn(existing, template))
        {
            foreach (var (key, value) in template.Options)
            {
                if (!ValuesEqual(existing.Options.GetValueOrDefault(key), value))
                {
                    existing.Options[key] = value;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool IsIncompleteBuiltIn(DownloadProfile existing, DownloadProfile template)
    {
        if (!IncompleteSignatures.TryGetValue(existing.Id, out var requiredKeys))
            return false;

        return requiredKeys.Any(key => !existing.Options.ContainsKey(key));
    }

    private static bool ValuesEqual(object? left, object? right) =>
        string.Equals(
            Convert.ToString(left, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToString(right, System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
}
