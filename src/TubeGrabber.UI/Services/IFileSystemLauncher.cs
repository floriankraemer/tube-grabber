// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

public interface IFileSystemLauncher
{
    bool TryOpenFile(string path);

    bool TryOpenLocation(string path);

    bool TryOpenUrl(string url);
}
