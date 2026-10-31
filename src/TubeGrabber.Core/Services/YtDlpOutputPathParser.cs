// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

public sealed class YtDlpOutputPathParser
{
    public bool TryAddCandidate(string line, ICollection<string> paths)
    {
        if (!TryExtractPath(line, out var path))
            return false;

        paths.Add(path);
        return true;
    }

    public bool TryExtractPath(string line, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        line = ConsoleOutputSanitizer.StripAnsi(line).Trim();
        if (line.Length == 0 || line[0] != '[')
            return false;

        const string destinationMarker = "Destination:";
        var destinationIndex = line.IndexOf(destinationMarker, StringComparison.OrdinalIgnoreCase);
        if (destinationIndex >= 0)
        {
            path = Unquote(line[(destinationIndex + destinationMarker.Length)..].Trim());
            return !string.IsNullOrWhiteSpace(path);
        }

        if (line.Contains("[MoveFiles]", StringComparison.OrdinalIgnoreCase)
            && line.Contains(" to ", StringComparison.OrdinalIgnoreCase))
        {
            var moveIndex = line.LastIndexOf(" to ", StringComparison.OrdinalIgnoreCase);
            path = Unquote(line[(moveIndex + " to ".Length)..].Trim());
            return !string.IsNullOrWhiteSpace(path);
        }

        foreach (var marker in new[] { " into ", " to " })
        {
            var markerIndex = line.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                continue;

            path = Unquote(line[(markerIndex + marker.Length)..].Trim());
            if (!string.IsNullOrWhiteSpace(path))
                return true;
        }

        path = string.Empty;
        return false;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];

        return value;
    }
}
