// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

public sealed class ExtraArgsTokenizer
{
    public IReadOnlyList<string> Tokenize(string extraArgs)
    {
        if (string.IsNullOrWhiteSpace(extraArgs))
            return [];

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteChar = '"';

        for (var i = 0; i < extraArgs.Length; i++)
        {
            var c = extraArgs[i];

            if (c is '"' or '\'')
            {
                if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    continue;
                }

                if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                    continue;
                }
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                FlushToken(current, tokens);
                continue;
            }

            current.Append(c);
        }

        if (inQuotes)
            throw new InvalidOperationException("Extra arguments contain unbalanced quotes.");

        FlushToken(current, tokens);
        return tokens;
    }

    private static void FlushToken(System.Text.StringBuilder current, List<string> tokens)
    {
        if (current.Length == 0)
            return;

        tokens.Add(current.ToString());
        current.Clear();
    }
}
