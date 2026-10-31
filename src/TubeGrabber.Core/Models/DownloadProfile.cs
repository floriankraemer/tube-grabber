// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class DownloadProfile
{
    public int SchemaVersion { get; set; } = Constants.SchemaVersions.Profile;
    public required string Id { get; init; }
    public required string Name { get; set; }
    public Dictionary<string, object?> Options { get; set; } = new(StringComparer.Ordinal);
    public string ExtraArgs { get; set; } = string.Empty;
}
