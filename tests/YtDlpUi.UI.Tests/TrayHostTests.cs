// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.UI.Services;

namespace YtDlpUi.UI.Tests;

public sealed class TrayHostTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldHideOnClose_RequiresPreferenceAndTray(bool runInBackground, bool tray, bool expected) =>
        Assert.Equal(expected, TrayHost.ShouldHideOnClose(runInBackground, tray));

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void ShouldShowWindowOnStart_OnlyHidesWhenTrayCanRestoreIt(bool minimized, bool tray, bool expected) =>
        Assert.Equal(expected, TrayHost.ShouldShowWindowOnStart(minimized, tray));
}
