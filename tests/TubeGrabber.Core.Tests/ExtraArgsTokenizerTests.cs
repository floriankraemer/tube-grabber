// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ExtraArgsTokenizerTests
{
    private readonly ExtraArgsTokenizer _tokenizer = new();

    [Fact]
    public void Tokenize_Empty_ReturnsEmpty()
    {
        Assert.Empty(_tokenizer.Tokenize(string.Empty));
    }

    [Fact]
    public void Tokenize_SplitsWhitespace()
    {
        var tokens = _tokenizer.Tokenize("--simulate  --verbose");
        Assert.Equal(["--simulate", "--verbose"], tokens);
    }

    [Fact]
    public void Tokenize_RespectsQuotes()
    {
        var tokens = _tokenizer.Tokenize("--postprocessor-args \"ffmpeg:-c copy\"");
        Assert.Equal(["--postprocessor-args", "ffmpeg:-c copy"], tokens);
    }

    [Fact]
    public void Tokenize_UnbalancedQuotes_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _tokenizer.Tokenize("\"unclosed"));
    }
}
