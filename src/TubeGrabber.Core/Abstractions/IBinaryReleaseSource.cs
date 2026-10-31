// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Abstractions;

public interface IBinaryReleaseSource
{
    ReleaseAsset GetYtDlpAsset(string runtimeIdentifier);
    ReleaseAsset GetFfmpegAsset(string runtimeIdentifier);
    ReleaseAsset GetDenoAsset(string runtimeIdentifier);
}
