// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Abstractions;

public interface IDesktopNotifier
{
    /// <summary>Shows an OS notification. Implementations never throw.</summary>
    Task ShowAsync(string title, string body, CancellationToken cancellationToken = default);
}
