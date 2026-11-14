// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Reflection;

namespace TubeGrabber.UI.Services;

/// <summary>The version stamped into the build (<c>-p:InformationalVersion</c> in CI), for <c>--version</c>.</summary>
internal static class AppVersion
{
    public static string Current => Read(typeof(AppVersion).Assembly);

    internal static string Read(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
