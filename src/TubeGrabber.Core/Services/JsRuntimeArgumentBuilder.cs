// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public static class JsRuntimeArgumentBuilder
{
    public static string? Build(AppConfiguration config, IJsRuntimeLocator locator)
    {
        if (string.IsNullOrWhiteSpace(config.JsRuntimeEngine)
            || !JsRuntimeEngines.IsSupported(config.JsRuntimeEngine))
            return null;

        var engine = config.JsRuntimeEngine.Trim().ToLowerInvariant();
        var path = locator.ResolvePath(config);
        return string.IsNullOrWhiteSpace(path) ? engine : $"{engine}:{path}";
    }
}
