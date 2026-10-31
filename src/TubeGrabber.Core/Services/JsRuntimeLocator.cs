// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class JsRuntimeLocator : IJsRuntimeLocator
{
    public string? ResolvePath(AppConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.JsRuntimeEngine)
            || !JsRuntimeEngines.IsSupported(config.JsRuntimeEngine))
            return null;

        var configured = ExecutablePathResolver.NormalizeIfExists(config.JsRuntimePath);
        if (configured is not null)
            return configured;

        return ExecutablePathResolver.FindOnPath(JsRuntimeEngines.GetExecutableName(config.JsRuntimeEngine));
    }
}
