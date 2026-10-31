// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.RegularExpressions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

/// <summary>Builds the one-line description of a profile shown on its card and detail header.</summary>
public static partial class ProfileSummaryBuilder
{
    public const string NoChangesText = "yt-dlp defaults";

    public static bool IsAudioOnly(DownloadProfile profile) =>
        ProfileOptionReader.IsTruthy(ProfileOptionValues.Normalize(profile.Options.GetValueOrDefault("-x")));

    /// <param name="includeCount">False when the number of options is shown separately, as in the detail header.</param>
    public static string Build(DownloadProfile profile, YtDlpOptionCatalog catalog, bool includeCount = true)
    {
        var changed = ProfileOptionValues.CountChanged(profile, catalog);
        var hasExtraArgs = !string.IsNullOrWhiteSpace(profile.ExtraArgs);
        if (changed == 0 && !hasExtraArgs)
            return NoChangesText;

        var parts = new List<string>();
        if (IsAudioOnly(profile))
        {
            var format = Text(profile, "--audio-format");
            parts.Add($"Audio only → {(format.Length > 0 ? format.ToUpperInvariant() : "best")}");
        }
        else
        {
            var container = Text(profile, "--merge-output-format");
            var video = container.Length > 0 ? container.ToUpperInvariant() : "Video";
            parts.Add(MaxHeight(profile) is { } height ? $"{video} up to {height}p" : video);
        }

        if (Text(profile, "--sponsorblock-remove").Length > 0)
            parts.Add("SponsorBlock");

        if (Text(profile, "--cookies").Length > 0
            || Text(profile, "--username").Length > 0
            || ProfileOptionReader.IsTruthy(ProfileOptionValues.Normalize(profile.Options.GetValueOrDefault("--netrc"))))
            parts.Add("signed in");

        if (hasExtraArgs)
            parts.Add("extra arguments");

        if (changed > 0 && includeCount)
            parts.Add(changed == 1 ? "1 option set" : $"{changed} options set");

        return string.Join(" · ", parts);
    }

    private static string Text(DownloadProfile profile, string flag) =>
        ProfileOptionReader.ReadString(ProfileOptionValues.Normalize(profile.Options.GetValueOrDefault(flag)))?.Trim() ?? string.Empty;

    private static int? MaxHeight(DownloadProfile profile)
    {
        var sort = SortResolution().Match(Text(profile, "-S"));
        if (sort.Success)
            return int.Parse(sort.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        var format = FormatHeight().Match(Text(profile, "-f"));
        return format.Success ? int.Parse(format.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"(?:^|,)res:(\d+)")]
    private static partial Regex SortResolution();

    [GeneratedRegex(@"height\s*<=\s*(\d+)")]
    private static partial Regex FormatHeight();
}
