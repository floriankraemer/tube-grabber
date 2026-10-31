// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;

namespace TubeGrabber.UI.Services.Notifications;

public sealed class NullDesktopNotifier : IDesktopNotifier
{
    public Task ShowAsync(string title, string body, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
