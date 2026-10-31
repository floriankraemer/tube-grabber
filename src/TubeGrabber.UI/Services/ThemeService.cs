// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia;
using Avalonia.Styling;
using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.Services;

public static class ThemeService
{
    public static ThemeVariant MapToVariant(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public static void Apply(ThemePreference preference)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = MapToVariant(preference);
    }
}
