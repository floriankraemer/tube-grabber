// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Constants;

public static class JsRuntimeEngines
{
    public const string None = "";

    public const string Deno = "deno";
    public const string Node = "node";
    public const string QuickJs = "quickjs";
    public const string QuickJsNg = "quickjs-ng";
    public const string Bun = "bun";

    public static IReadOnlyList<JsRuntimeEngineDefinition> All { get; } =
    [
        new(None, "Default (yt-dlp built-in)", "Only Deno is enabled by default in yt-dlp; leave blank to use its built-in lookup."),
        new(Deno, "Deno (recommended)", "https://deno.com/ — minimum version 2.0.0"),
        new(Node, "Node.js", "https://nodejs.org/ — minimum version 20.0.0"),
        new(QuickJs, "QuickJS", "https://bellard.org/quickjs/ — minimum version 2023-12-9"),
        new(QuickJsNg, "QuickJS-ng", "https://quickjs-ng.github.io/quickjs/"),
        new(Bun, "Bun", "https://bun.com/ — minimum version 1.0.31"),
    ];

    public static bool IsSupported(string? engineId) =>
        !string.IsNullOrWhiteSpace(engineId)
        && All.Any(engine => string.Equals(engine.Id, engineId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(engine.Id));

    public static JsRuntimeEngineDefinition? Find(string? engineId) =>
        string.IsNullOrWhiteSpace(engineId)
            ? All[0]
            : All.FirstOrDefault(engine => string.Equals(engine.Id, engineId, StringComparison.OrdinalIgnoreCase));

    public static string GetExecutableName(string engineId) =>
        engineId.ToLowerInvariant() switch
        {
            Deno => "deno",
            Node => "node",
            Bun => "bun",
            QuickJs => "qjs",
            QuickJsNg => "qjs-ng",
            _ => engineId,
        };
}

public sealed record JsRuntimeEngineDefinition(string Id, string DisplayName, string Description);
