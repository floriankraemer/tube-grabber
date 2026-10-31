// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Abstractions;

public interface IBinaryLocator
{
    string? ResolveYtDlpPath(AppConfiguration config);

    string? ResolveFfmpegPath(AppConfiguration config);

    string GetBundledYtDlpPath();

    string GetBundledFfmpegPath();

    string GetBundledDenoPath();
}
