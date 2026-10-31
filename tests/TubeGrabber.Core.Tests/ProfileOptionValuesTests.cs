// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ProfileOptionValuesTests
{
    private static readonly YtDlpOptionCatalog Catalog = new();

    private static DownloadProfile Profile(string name = "P") => new() { Id = "p", Name = name };

    [Fact]
    public void AreEquivalent_ExplicitDefaultEqualsMissingKey()
    {
        var withDefault = Profile();
        withDefault.Options["--retries"] = 10;
        withDefault.Options["-x"] = false;
        Assert.True(ProfileOptionValues.AreEquivalent(withDefault, Profile(), Catalog));
    }

    [Fact]
    public void AreEquivalent_DetectsChangedValueNameAndExtraArgs()
    {
        var changed = Profile();
        changed.Options["--retries"] = 3;
        Assert.False(ProfileOptionValues.AreEquivalent(changed, Profile(), Catalog));
        Assert.False(ProfileOptionValues.AreEquivalent(Profile("Other"), Profile(), Catalog));
        var extra = Profile();
        extra.ExtraArgs = "--x";
        Assert.False(ProfileOptionValues.AreEquivalent(extra, Profile(), Catalog));
    }

    [Fact]
    public void AreEquivalent_NumberAndTextFormsMatch()
    {
        var number = Profile();
        number.Options["--retries"] = 3;
        var text = Profile();
        text.Options["--retries"] = "3";
        Assert.True(ProfileOptionValues.AreEquivalent(number, text, Catalog));
    }

    [Fact]
    public void IsChanged_BoolJsonElement_IsRead()
    {
        var profile = System.Text.Json.JsonSerializer.Deserialize<DownloadProfile>(
            """{"Id":"p","Name":"P","Options":{"-x":true}}""")!;
        Assert.True(ProfileOptionValues.IsChanged(Catalog.FindByFlag("-x")!, profile.Options["-x"]));
    }

    [Fact]
    public void Clone_NormalizesJsonElementsAndDoesNotShareOptions()
    {
        var profile = System.Text.Json.JsonSerializer.Deserialize<DownloadProfile>(
            """{"Id":"p","Name":"P","Options":{"-x":true}}""")!;
        var clone = ProfileOptionValues.Clone(profile);
        Assert.Equal(true, clone.Options["-x"]);
        clone.Options["-x"] = false;
        Assert.NotEqual(true, clone.Options["-x"]);
        Assert.IsType<System.Text.Json.JsonElement>(profile.Options["-x"]);
    }
}
