// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class YtDlpOptionDefinition
{
    public required string Flag { get; init; }
    /// <summary>Short human name shown as the row title; the flag is shown beside it.</summary>
    public string Label { get; init; } = string.Empty;
    public required string Section { get; init; }
    public required string ValueType { get; init; }
    public string? DefaultValue { get; init; }
    public required string Tooltip { get; init; }
    public IReadOnlyList<string> Choices { get; init; } = [];
}
