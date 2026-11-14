// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.UI.Tests;

// Binary lookup that never consults the real PATH, so tests behave the same on any machine.
internal static class TestBinaryLocator
{
    public static BinaryLocator Create(string configRoot) => new(configRoot, _ => null);
}
