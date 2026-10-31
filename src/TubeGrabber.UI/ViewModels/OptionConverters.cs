// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Data.Converters;

namespace TubeGrabber.UI.ViewModels;

public static class OptionConverters
{
    /// <summary>Choice lists start with an empty entry; show it as "Not set".</summary>
    public static readonly IValueConverter NotSet =
        new FuncValueConverter<string?, string>(text => string.IsNullOrEmpty(text) ? "Not set" : text);
}
