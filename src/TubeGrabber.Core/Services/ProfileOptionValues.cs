// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Globalization;
using System.Text.Json;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

/// <summary>
/// Compares stored profile option values with the catalog defaults. A missing key, a null and an explicit default
/// all count as "not changed", so resetting an option never makes a profile look different from one that never set it.
/// </summary>
public static class ProfileOptionValues
{
    /// <summary>Unwraps values that came back from JSON as <see cref="JsonElement"/> into bool, number or string.</summary>
    public static object? Normalize(object? value) =>
        value is JsonElement element ? ProfileOptionReader.ConvertJsonElement(element) : value;

    public static bool IsBool(YtDlpOptionDefinition definition) =>
        string.Equals(definition.ValueType, "bool", StringComparison.OrdinalIgnoreCase);

    /// <summary>The comparable text of the value the option effectively has: the stored value, or the default when unset.</summary>
    public static string EffectiveText(YtDlpOptionDefinition definition, object? stored)
    {
        var value = Normalize(stored);
        if (value is null)
            return DefaultText(definition);

        return IsBool(definition)
            ? ProfileOptionReader.IsTruthy(value) ? "true" : "false"
            : ProfileOptionReader.ReadString(value) ?? string.Empty;
    }

    public static string DefaultText(YtDlpOptionDefinition definition) =>
        IsBool(definition)
            ? bool.TryParse(definition.DefaultValue, out var on) && on ? "true" : "false"
            : definition.DefaultValue ?? string.Empty;

    public static bool IsChanged(YtDlpOptionDefinition definition, object? stored) =>
        !string.Equals(EffectiveText(definition, stored), DefaultText(definition), StringComparison.Ordinal);

    /// <summary>Number of catalog options that differ from their default in the profile.</summary>
    public static int CountChanged(DownloadProfile profile, YtDlpOptionCatalog catalog) =>
        catalog.GetAll().Count(definition => IsChanged(definition, profile.Options.GetValueOrDefault(definition.Flag)));

    /// <summary>True when both profiles would run yt-dlp the same way and carry the same name.</summary>
    public static bool AreEquivalent(DownloadProfile left, DownloadProfile right, YtDlpOptionCatalog catalog)
    {
        if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            || !string.Equals(left.ExtraArgs, right.ExtraArgs, StringComparison.Ordinal))
            return false;

        foreach (var definition in catalog.GetAll())
        {
            if (!string.Equals(
                    EffectiveText(definition, left.Options.GetValueOrDefault(definition.Flag)),
                    EffectiveText(definition, right.Options.GetValueOrDefault(definition.Flag)),
                    StringComparison.Ordinal))
                return false;
        }

        // Keys the catalog does not know are kept verbatim, so they must match literally.
        var unknown = left.Options.Keys.Concat(right.Options.Keys)
            .Where(key => catalog.FindByFlag(key) is null)
            .Distinct(StringComparer.Ordinal);
        return unknown.All(key => string.Equals(
            Convert.ToString(Normalize(left.Options.GetValueOrDefault(key)), CultureInfo.InvariantCulture),
            Convert.ToString(Normalize(right.Options.GetValueOrDefault(key)), CultureInfo.InvariantCulture),
            StringComparison.Ordinal));
    }

    public static DownloadProfile Clone(DownloadProfile source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        ExtraArgs = source.ExtraArgs,
        SchemaVersion = source.SchemaVersion,
        Options = source.Options.ToDictionary(pair => pair.Key, pair => Normalize(pair.Value), StringComparer.Ordinal),
    };
}
