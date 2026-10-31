// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class BinaryInstallResult
{
    public bool IsSuccess { get; init; }
    public string? InstalledPath { get; init; }
    public string? Error { get; init; }

    public static BinaryInstallResult Success(string path) =>
        new() { IsSuccess = true, InstalledPath = path };

    public string? ManualDownloadUrl { get; init; }

    public static BinaryInstallResult Failure(string error, string? manualDownloadUrl = null) =>
        new() { IsSuccess = false, Error = error, ManualDownloadUrl = manualDownloadUrl };
}
