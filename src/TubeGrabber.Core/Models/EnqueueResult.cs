// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class EnqueueResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public DownloadJob? Job { get; init; }

    public static EnqueueResult Success(DownloadJob job) =>
        new() { IsSuccess = true, Job = job };

    public static EnqueueResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
