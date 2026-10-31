// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ConfigFilePermissionsTests
{
    [Fact]
    public void ApplyRestrictedPermissions_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tube-grabber-perm-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{}");
        try
        {
            ConfigFilePermissions.ApplyRestrictedPermissions(path, isDirectory: false);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
