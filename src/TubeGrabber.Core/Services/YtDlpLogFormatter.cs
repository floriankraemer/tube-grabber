// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class YtDlpLogFormatter
{
    public static string Format(YtDlpInvocation invocation, YtDlpRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormatCommandLine(invocation));
        builder.AppendLine();
        AppendStream(builder, "stdout", result.StandardOutput);
        AppendStream(builder, "stderr", result.StandardError);

        if (result.WasCancelled)
            builder.AppendLine("[cancelled]");

        builder.AppendLine($"[exit code: {result.ExitCode}]");
        return builder.ToString().TrimEnd();
    }

    public static string Format(YtDlpInvocation invocation, Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormatCommandLine(invocation));
        builder.AppendLine();
        builder.Append(exception);
        return builder.ToString().TrimEnd();
    }

    public static string FormatCommandLine(YtDlpInvocation invocation)
    {
        var args = string.Join(' ', invocation.Arguments.Select(QuoteArgument));
        return $"$ {QuoteArgument(invocation.ExecutablePath)} {args}".TrimEnd();
    }

    private static void AppendStream(StringBuilder builder, string label, string content)
    {
        if (string.IsNullOrEmpty(content))
            return;

        builder.AppendLine($"--- {label} ---");
        builder.AppendLine(content.TrimEnd());
        builder.AppendLine();
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('"', StringComparison.Ordinal)
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
}
