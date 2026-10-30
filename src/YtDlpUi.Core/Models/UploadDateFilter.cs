// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Models;

/// <summary>Values match YouTube's own "Upload date" search filter.</summary>
public enum UploadDateFilter
{
    Any = 0,
    LastHour = 1,
    Today = 2,
    ThisWeek = 3,
    ThisMonth = 4,
    ThisYear = 5,
}
