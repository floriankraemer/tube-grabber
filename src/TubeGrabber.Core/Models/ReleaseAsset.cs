// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class ReleaseAsset
{
    public required string DownloadUrl { get; init; }
    public required string FileName { get; init; }
    public required string ReleasePageUrl { get; init; }
    public string? Sha256 { get; init; }
    public long MinimumSizeBytes { get; init; }
}
