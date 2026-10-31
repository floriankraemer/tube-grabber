// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public enum DownloadStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}
