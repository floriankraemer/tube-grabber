// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

public static class BinaryInstallMessages
{
    public static string FormatFailure(
        string displayName,
        string runtimeIdentifier,
        string cause,
        string releasePageUrl) =>
        $"Could not download {displayName} for {runtimeIdentifier}: {cause}. " +
        $"Download the correct file manually from {releasePageUrl}, then use Browse to select it.";

    public static string FormatUnsupportedPlatform(
        string displayName,
        string runtimeIdentifier,
        string releasePageUrl) =>
        $"Automatic install of {displayName} is not supported on {runtimeIdentifier}. " +
        $"Download a build manually from {releasePageUrl}, then use Browse to select it.";

    public static string FormatBrowserOpenFailure() =>
        "Could not open your browser — use the Releases button or copy the URL above.";
}
