// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class JsRuntimeArgumentBuilderTests : IDisposable
{
    private readonly string _root;

    public JsRuntimeArgumentBuilderTests() =>
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_ReturnsNullWhenEngineNotSelected()
    {
        var config = new AppConfiguration();
        Assert.Null(JsRuntimeArgumentBuilder.Build(config, new JsRuntimeLocator()));
    }

    [Fact]
    public void Build_ReturnsEngineWithPath()
    {
        Directory.CreateDirectory(_root);
        var denoPath = Path.Combine(_root, "deno");
        File.WriteAllText(denoPath, string.Empty);

        var config = new AppConfiguration
        {
            JsRuntimeEngine = JsRuntimeEngines.Deno,
            JsRuntimePath = denoPath,
        };

        var argument = JsRuntimeArgumentBuilder.Build(config, new JsRuntimeLocator());
        Assert.Equal($"deno:{Path.GetFullPath(denoPath)}", argument);
    }

    [Fact]
    public void Build_IncludesJsRuntimesInCommand()
    {
        Directory.CreateDirectory(_root);
        var denoPath = Path.Combine(_root, "deno");
        File.WriteAllText(denoPath, string.Empty);

        var config = new AppConfiguration
        {
            JsRuntimeEngine = JsRuntimeEngines.Deno,
            JsRuntimePath = denoPath,
        };

        var argument = JsRuntimeArgumentBuilder.Build(config, new JsRuntimeLocator());
        var builder = new YtDlpCommandBuilder(new YtDlpOptionCatalog(), new ExtraArgsTokenizer());
        var profile = new DownloadProfile { Id = "p", Name = "P" };
        var args = builder.Build(profile, null, argument, "https://www.youtube.com/watch?v=abc");

        Assert.Contains("--js-runtimes", args);
        Assert.Contains($"deno:{Path.GetFullPath(denoPath)}", args);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
