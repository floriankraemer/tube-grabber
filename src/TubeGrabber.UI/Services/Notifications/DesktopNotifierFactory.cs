// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;

namespace TubeGrabber.UI.Services.Notifications;

public static class DesktopNotifierFactory
{
    public static IDesktopNotifier Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsToastNotifier();
        }

        if (OperatingSystem.IsLinux())
        {
            return new FreeDesktopNotifier();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsNotifier();
        }

        return new NullDesktopNotifier();
    }
}
