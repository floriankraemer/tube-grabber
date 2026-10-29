// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.UI.Services;

public interface ILoginItemService
{
    void SetEnabled(bool enabled);

    bool IsEnabled();
}
