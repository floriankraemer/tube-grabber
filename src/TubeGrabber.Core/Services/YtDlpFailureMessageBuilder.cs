// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text;
using System.Text.RegularExpressions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class YtDlpFailureMessageBuilder
{
    private const int MaxMessageLength = 500;

    private static readonly Regex ErrorLineRegex = new(
        @"^(?:ERROR|Error|WARNING):\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static string Build(YtDlpRunResult result)
    {
        var combined = CombineStreams(result.StandardOutput, result.StandardError);
        if (string.IsNullOrWhiteSpace(combined))
            return $"yt-dlp exited with code {result.ExitCode}.";

        var errors = ExtractLines(combined, "ERROR");
        if (errors.Count > 0)
            return Truncate(string.Join(Environment.NewLine, errors));

        var warnings = ExtractLines(combined, "WARNING");
        if (warnings.Count > 0)
            return Truncate(string.Join(Environment.NewLine, warnings));

        var matched = ErrorLineRegex.Matches(combined)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (matched.Count > 0)
            return Truncate(string.Join(Environment.NewLine, matched));

        return Truncate(combined.Trim());
    }

    private static string CombineStreams(string stdout, string stderr)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(stdout))
            builder.AppendLine(stdout.TrimEnd());

        if (!string.IsNullOrWhiteSpace(stderr))
            builder.AppendLine(stderr.TrimEnd());

        return builder.ToString().Trim();
    }

    private static List<string> ExtractLines(string text, string level)
    {
        var prefix = level + ":";
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(line => line[prefix.Length..].Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string Truncate(string message) =>
        message.Length <= MaxMessageLength ? message : message[..MaxMessageLength] + "…";
}
