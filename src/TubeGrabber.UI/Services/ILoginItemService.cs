// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

public interface ILoginItemService
{
    void SetEnabled(bool enabled);

    bool IsEnabled();

    /// <summary>Replaces a start-at-login entry registered under the application's former name with the current one.</summary>
    void MigrateLegacyEntry();
}
