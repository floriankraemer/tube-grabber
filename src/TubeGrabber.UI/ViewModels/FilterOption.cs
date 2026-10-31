// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.ViewModels;

public sealed record FilterOption<T>(string Label, T Value)
{
    // ComboBox renders items via ToString().
    public override string ToString() => Label;
}
