// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

public sealed record SearchFilters(
    UploadDateFilter UploadDate = UploadDateFilter.Any,
    DurationFilter Duration = DurationFilter.Any,
    bool HideShorts = true)
{
    public static SearchFilters Default { get; } = new();
}
