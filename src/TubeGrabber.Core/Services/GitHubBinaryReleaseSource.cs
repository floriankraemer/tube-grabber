// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class GitHubBinaryReleaseSource : IBinaryReleaseSource
{
    public ReleaseAsset GetYtDlpAsset(string runtimeIdentifier) =>
        PlatformBinaryAssets.GetYtDlpAsset(runtimeIdentifier);

    public ReleaseAsset GetFfmpegAsset(string runtimeIdentifier) =>
        PlatformBinaryAssets.GetFfmpegAsset(runtimeIdentifier);

    public ReleaseAsset GetDenoAsset(string runtimeIdentifier) =>
        PlatformBinaryAssets.GetDenoAsset(runtimeIdentifier);
}
