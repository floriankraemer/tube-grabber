// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void Current_IsNotEmpty() => Assert.False(string.IsNullOrWhiteSpace(AppVersion.Current));

    [Fact]
    public void Read_PrefersTheStampedInformationalVersion() =>
        Assert.Equal(typeof(AppVersionTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
            AppVersion.Read(typeof(AppVersionTests).Assembly));
}
