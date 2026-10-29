// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace YtDlpUi.Core.Abstractions;

public interface IDesktopNotifier
{
    Task ShowAsync(string title, string body, CancellationToken cancellationToken = default);
}
