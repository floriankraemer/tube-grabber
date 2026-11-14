// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

/// <summary>
/// Tells a single click from the first click of a double click. A click only counts as single once the
/// double-click interval has passed without a second one; a double click cancels the pending single.
/// </summary>
public sealed class ClickArbiter(
    Func<TimeSpan> doubleClickInterval,
    Func<TimeSpan, Action, IDisposable> schedule,
    Action onSingleClick,
    Action onDoubleClick)
{
    private IDisposable? _pending;

    public void Click()
    {
        Cancel();
        _pending = schedule(doubleClickInterval(), () =>
        {
            _pending = null;
            onSingleClick();
        });
    }

    public void DoubleClick()
    {
        Cancel();
        onDoubleClick();
    }

    /// <summary>Drops a pending single click, for example when the view goes away.</summary>
    public void Cancel()
    {
        _pending?.Dispose();
        _pending = null;
    }
}
