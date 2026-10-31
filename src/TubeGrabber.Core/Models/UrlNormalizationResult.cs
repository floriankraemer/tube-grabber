// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed class UrlNormalizationResult
{
    public bool IsSuccess { get; init; }
    public string? NormalizedUrl { get; init; }
    public string? Error { get; init; }

    public static UrlNormalizationResult Success(string url) =>
        new() { IsSuccess = true, NormalizedUrl = url };

    public static UrlNormalizationResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
