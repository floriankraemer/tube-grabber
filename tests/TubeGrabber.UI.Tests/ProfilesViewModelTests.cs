// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class ProfilesViewModelTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _root;
    private readonly ProfileStore _store;
    private readonly SettingsCoordinator _coordinator;
    private readonly ISubscriptionMonitor _monitor = Substitute.For<ISubscriptionMonitor>();
    private readonly ProfilesViewModel _viewModel;

    public ProfilesViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tube-grabber-tests", Guid.NewGuid().ToString("N"));
        _store = new ProfileStore(_root);
        var appConfig = new AppConfigStore(_root, _store);
        var catalog = new YtDlpOptionCatalog();
        _coordinator = new SettingsCoordinator(
            appConfig,
            _store,
            new YtDlpCommandBuilder(catalog, new ExtraArgsTokenizer()),
            ViewModelTestHelpers.CreateValidator(),
            new BinaryLocator(_root),
            new JsRuntimeLocator());
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<ChannelSubscription>());
        _viewModel = ViewModelTestHelpers.CreateProfilesViewModel(_coordinator, _store, _monitor);
    }

    private async Task LoadAsync()
    {
        var (config, profiles) = await _coordinator.LoadAsync(Ct);
        await _viewModel.LoadAsync(config, profiles);
    }

    private OptionItemViewModel Option(string flag) =>
        _viewModel.OptionSections.SelectMany(s => s.Options).First(o => o.Flag == flag);

    [Fact]
    public async Task LoadAsync_ShowsTheListWithBuiltInProfilesSortedByName()
    {
        await LoadAsync();

        Assert.True(_viewModel.IsListMode);
        Assert.Equal(["Default", "Download Audio as mp3", "Download HQ Video"], _viewModel.Cards.Select(c => c.Name));
        Assert.All(_viewModel.Cards, c => Assert.True(c.IsBuiltIn));
        Assert.Equal(0, _viewModel.DirtyProfileCount);
        Assert.Equal(ProfileCardViewModel.DownloadIcon, _viewModel.Cards[0].IconKey);
        Assert.Equal(ProfileCardViewModel.MusicIcon, _viewModel.Cards[1].IconKey);
        Assert.Equal(ProfileCardViewModel.FilmIcon, _viewModel.Cards[2].IconKey);
        Assert.StartsWith("Audio only → MP3", _viewModel.Cards[1].Summary);
    }

    [Fact]
    public async Task LoadAsync_CountsSubscriptionsPerProfile()
    {
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ChannelSubscription { ChannelId = "a", ChannelName = "A", ChannelUrl = "u", ProfileId = "hq-video" },
            new ChannelSubscription { ChannelId = "b", ChannelName = "B", ChannelUrl = "u", ProfileId = "hq-video" },
        });

        await LoadAsync();

        Assert.Equal("Used by 2 subscriptions", _viewModel.Cards.Single(c => c.Id == "hq-video").UsedByText);
        Assert.Equal(string.Empty, _viewModel.Cards.Single(c => c.Id == "default").UsedByText);
    }

    [Fact]
    public async Task EditingAnOption_MarksOnlyThatProfileDirty_AndRevertingClearsIt()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        Option("--retries").StringValue = "3";

        Assert.Equal(1, _viewModel.DirtyProfileCount);
        Assert.True(_viewModel.Cards.Single(c => c.Id == "default").IsDirty);
        Assert.False(_viewModel.Cards.Single(c => c.Id == "hq-video").IsDirty);
        Assert.Equal("Unsaved changes in 1 profile", _viewModel.UnsavedSummary);

        Option("--retries").StringValue = "10";

        Assert.Equal(0, _viewModel.DirtyProfileCount);
        Assert.Equal(string.Empty, _viewModel.UnsavedSummary);
    }

    [Fact]
    public async Task OpeningAProfile_WithoutChangingIt_DoesNotMakeItDirty()
    {
        await LoadAsync();
        _viewModel.OpenProfile("audio-mp3");
        _viewModel.CloseProfile();
        _viewModel.OpenProfile("hq-video");

        Assert.Equal(0, _viewModel.DirtyProfileCount);
        Assert.Empty(_viewModel.GetDirtyDrafts());
    }

    [Fact]
    public async Task Back_KeepsTheDraftSoTheListShowsItAsUnsaved()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        _viewModel.ExtraArgs = "--no-mtime";
        _viewModel.CloseProfile();

        Assert.True(_viewModel.IsListMode);
        Assert.True(_viewModel.Cards.Single(c => c.Id == "default").IsDirty);
        _viewModel.OpenProfile("default");
        Assert.Equal("--no-mtime", _viewModel.ExtraArgs);
    }

    [Fact]
    public async Task ResettingAnOption_WritesTheDefaultExplicitly_InsteadOfRemovingTheKey()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        Assert.True(Option("--force-overwrites").IsChanged);

        Option("--force-overwrites").Reset();

        Assert.False(Option("--force-overwrites").IsChanged);
        var draft = Assert.Single(_viewModel.GetDirtyDrafts());
        Assert.Equal(false, draft.Options["--force-overwrites"]);
    }

    [Fact]
    public async Task ResettingAChangedValueBackToUnset_DoesNotLeaveTheProfileDirty()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");
        Option("--proxy").StringValue = "socks5://x";
        Assert.Equal(1, _viewModel.DirtyProfileCount);

        Option("--proxy").Reset();

        Assert.Equal(0, _viewModel.DirtyProfileCount);
    }

    [Fact]
    public async Task OptionRows_ReportChangedStateAndCounts()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");

        var format = _viewModel.OptionSections.Single(s => s.Name == "Video Format");
        Assert.Equal(3, format.ChangedCount);
        Assert.True(format.IsExpanded);
        Assert.False(_viewModel.OptionSections.Single(s => s.Name == "Authentication").IsExpanded);
        Assert.Equal(_viewModel.EditingProfile!.ChangedCount, _viewModel.ChangedCount);
        Assert.Equal("3 changed", format.ChangedText);

        Option("--remux-video").StringValue = "mkv";

        Assert.Equal(4, format.ChangedCount);
    }

    [Fact]
    public async Task Sections_WithChangesComeFirstMostChangedFirst_TheRestStayAlphabetical()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");

        var names = _viewModel.OptionSections.Select(s => s.Name).ToList();
        var changed = _viewModel.OptionSections.TakeWhile(s => s.HasChanges).Select(s => s.Name).ToList();

        Assert.Equal(["Video Format", "Download"], changed);
        Assert.Equal(names.Skip(changed.Count).Order(StringComparer.OrdinalIgnoreCase), names.Skip(changed.Count));
        Assert.All(_viewModel.OptionSections.Skip(changed.Count), s => Assert.False(s.HasChanges));
    }

    [Fact]
    public async Task Duplicate_CopiesTheDraftNotTheSavedFile_WritesItAndOpensIt()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        _viewModel.ExtraArgs = "--no-mtime";

        Assert.True(await _viewModel.DuplicateProfileAsync("default"));

        var copy = _viewModel.EditingProfile!;
        Assert.Equal("Default (copy)", copy.Name);
        Assert.False(copy.IsBuiltIn);
        var onDisk = await _store.GetAsync(copy.Id, Ct);
        Assert.Equal("--no-mtime", onDisk!.ExtraArgs);
        Assert.Equal("--no-mtime", _viewModel.ExtraArgs);
        Assert.False(copy.IsDirty);
        Assert.Equal("", (await _store.GetAsync("default", Ct))!.ExtraArgs);
    }

    [Fact]
    public async Task Duplicate_NumbersRepeatedCopies()
    {
        await LoadAsync();
        await _viewModel.DuplicateProfileAsync("default");
        await _viewModel.DuplicateProfileAsync("default");
        await _viewModel.DuplicateProfileAsync("default");

        Assert.Contains(_viewModel.Cards, c => c.Name == "Default (copy)");
        Assert.Contains(_viewModel.Cards, c => c.Name == "Default (copy 2)");
        Assert.Contains(_viewModel.Cards, c => c.Name == "Default (copy 3)");
    }

    [Fact]
    public async Task Create_FromYtDlpDefaults_StartsEmpty()
    {
        await LoadAsync();

        Assert.True(await _viewModel.CreateProfileAsync("Podcasts", sourceId: null));

        Assert.Equal("Podcasts", _viewModel.EditingProfile!.Name);
        Assert.Equal("yt-dlp defaults", _viewModel.EditingProfile.Summary);
        Assert.Empty((await _store.GetAsync(_viewModel.EditingProfile.Id, Ct))!.Options);
    }

    [Fact]
    public async Task Create_FromAProfile_CopiesItsUnsavedDraft()
    {
        await LoadAsync();
        _viewModel.OpenProfile("audio-mp3");
        Option("--audio-format").StringValue = "m4a";
        _viewModel.CloseProfile();

        await _viewModel.CreateProfileAsync("Mine", "audio-mp3");

        Assert.Equal("m4a", Option("--audio-format").StringValue);
        var onDisk = await _store.GetAsync(_viewModel.EditingProfile!.Id, Ct);
        Assert.Equal("m4a", onDisk!.Options["--audio-format"]?.ToString());
        Assert.Equal("Audio only → M4A", _viewModel.EditingProfile.Summary.Split(" · ")[0]);
    }

    [Fact]
    public async Task Create_RefusesDuplicateAndEmptyNames()
    {
        await LoadAsync();
        var failures = new List<string>();
        _viewModel.OperationFailed += (_, message) => failures.Add(message);

        Assert.False(await _viewModel.CreateProfileAsync("default", null));
        Assert.False(await _viewModel.CreateProfileAsync("   ", null));

        Assert.Equal(2, failures.Count);
        Assert.Equal(3, _viewModel.Cards.Count);
    }

    [Fact]
    public async Task Names_AreUniqueCaseInsensitively_AndTheOwnNameIsAllowed()
    {
        await LoadAsync();

        Assert.False(_viewModel.IsNameAvailable("download hq video"));
        Assert.False(_viewModel.IsNameAvailable("  Default  "));
        Assert.True(_viewModel.IsNameAvailable("Default", exceptId: "default"));
        Assert.Equal("Enter a name for the profile.", _viewModel.NameError(" "));
        Assert.NotNull(_viewModel.NameError("DEFAULT"));
        Assert.Null(_viewModel.NameError("Fresh"));
    }

    [Fact]
    public async Task SuggestNewName_IsAutoNumbered()
    {
        await LoadAsync();
        Assert.Equal("New profile", _viewModel.SuggestNewName());

        await _viewModel.CreateProfileAsync("New profile", null);
        Assert.Equal("New profile 2", _viewModel.SuggestNewName());
    }

    [Fact]
    public async Task DefaultSourceId_IsTheLastViewedProfile()
    {
        await LoadAsync();
        Assert.Equal("default", _viewModel.DefaultSourceId);

        _viewModel.OpenProfile("hq-video");
        _viewModel.CloseProfile();

        Assert.Equal("hq-video", _viewModel.DefaultSourceId);
    }

    [Fact]
    public async Task Rename_IsLockedForBuiltIns()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");

        Assert.False(_viewModel.CanRename);
        Assert.False(_viewModel.Rename("default", "Mine"));
        Assert.Equal("Default", _viewModel.Cards.Single(c => c.Id == "default").Name);
    }

    [Fact]
    public async Task Rename_ChangesTheDraftAndMarksItDirty_UntilSaved()
    {
        await LoadAsync();
        await _viewModel.CreateProfileAsync("Mine", null);
        var id = _viewModel.EditingProfile!.Id;

        Assert.True(_viewModel.CanRename);
        Assert.False(_viewModel.Rename(id, "DEFAULT"));
        Assert.True(_viewModel.Rename(id, "Renamed"));

        Assert.True(_viewModel.EditingProfile.IsDirty);
        Assert.Equal("Mine", (await _store.GetAsync(id, Ct))!.Name);
        var dirty = _viewModel.GetDirtyDrafts();
        await _coordinator.SaveAsync(new AppConfiguration(), dirty, Ct);
        _viewModel.MarkAllSaved();
        Assert.Equal("Renamed", (await _store.GetAsync(id, Ct))!.Name);
        Assert.False(_viewModel.EditingProfile.IsDirty);
    }

    [Fact]
    public async Task Delete_RefusesBuiltInProfiles()
    {
        await LoadAsync();

        Assert.False(_viewModel.CanDeleteProfile("default"));
        Assert.False(await _viewModel.DeleteProfileAsync("hq-video"));
        Assert.NotNull(await _store.GetAsync("hq-video", Ct));
    }

    [Fact]
    public async Task Delete_ReassignsSubscriptionsToDefault_RemovesTheFileAndReturnsToTheList()
    {
        await LoadAsync();
        await _viewModel.CreateProfileAsync("Mine", null);
        var id = _viewModel.EditingProfile!.Id;
        var affected = new ChannelSubscription { ChannelId = "a", ChannelName = "A", ChannelUrl = "u", ProfileId = id };
        var other = new ChannelSubscription { ChannelId = "b", ChannelName = "B", ChannelUrl = "u", ProfileId = "hq-video" };
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { affected, other });
        var changed = 0;
        _viewModel.ProfilesChanged += (_, _) => changed++;

        Assert.True(_viewModel.CanDelete);
        Assert.True(await _viewModel.DeleteProfileAsync(id));

        await _monitor.Received(1).SetProfileAsync(affected.Id, "default", Arg.Any<CancellationToken>());
        await _monitor.DidNotReceive().SetProfileAsync(other.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Null(await _store.GetAsync(id, Ct));
        Assert.True(_viewModel.IsListMode);
        Assert.DoesNotContain(_viewModel.Cards, c => c.Id == id);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Delete_DropsTheUnsavedDraftOfTheProfile()
    {
        await LoadAsync();
        await _viewModel.CreateProfileAsync("Mine", null);
        var id = _viewModel.EditingProfile!.Id;
        _viewModel.ExtraArgs = "--no-mtime";
        Assert.Equal(1, _viewModel.DirtyProfileCount);

        await _viewModel.DeleteProfileAsync(id);

        Assert.Equal(0, _viewModel.DirtyProfileCount);
    }

    [Fact]
    public async Task Delete_ReportsFailureWithoutLosingTheProfile()
    {
        await LoadAsync();
        await _viewModel.CreateProfileAsync("Mine", null);
        var id = _viewModel.EditingProfile!.Id;
        _monitor.ListAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ChannelSubscription { ChannelId = "a", ChannelName = "A", ChannelUrl = "u", ProfileId = id },
        });
        _monitor.SetProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("disk full"));
        string? failure = null;
        _viewModel.OperationFailed += (_, message) => failure = message;

        Assert.False(await _viewModel.DeleteProfileAsync(id));

        Assert.Contains("disk full", failure);
        Assert.NotNull(await _store.GetAsync(id, Ct));
        Assert.Contains(_viewModel.Cards, c => c.Id == id);
    }

    [Fact]
    public async Task Delete_IsRefusedForTheLastProfile()
    {
        await _store.SaveAsync(new DownloadProfile { Id = "only", Name = "Only" }, Ct);
        await _viewModel.LoadAsync(new AppConfiguration(), [(await _store.GetAsync("only", Ct))!]);

        Assert.False(_viewModel.CanDeleteProfile("only"));
        Assert.False(await _viewModel.DeleteProfileAsync("only"));
    }

    [Fact]
    public async Task ResetToBuiltIn_RestoresTemplateOptions_KeepsTheName_AndMarksDirty()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");
        Option("--merge-output-format").StringValue = "mkv";
        Option("--proxy").StringValue = "socks5://x";
        _viewModel.ExtraArgs = "--no-mtime";

        Assert.True(_viewModel.ResetToBuiltIn("hq-video"));

        Assert.Equal("mp4", Option("--merge-output-format").StringValue);
        Assert.Equal(string.Empty, Option("--proxy").StringValue);
        Assert.Equal(string.Empty, _viewModel.ExtraArgs);
        Assert.Equal("Download HQ Video", _viewModel.EditingProfile!.Name);
        Assert.False(_viewModel.EditingProfile.IsDirty);
    }

    [Fact]
    public async Task ResetToBuiltIn_OfAnAlreadySavedCustomisedProfile_IsDirtyUntilSaved()
    {
        await LoadAsync();
        var saved = (await _store.GetAsync("hq-video", Ct))!;
        saved.Options["--proxy"] = "socks5://x";
        await _store.SaveAsync(saved, Ct);
        await LoadAsync();

        _viewModel.ResetToBuiltIn("hq-video");

        Assert.Equal(1, _viewModel.DirtyProfileCount);
        Assert.False(_viewModel.ResetToBuiltIn("nope"));
    }

    [Fact]
    public async Task ResetToBuiltIn_IsNotOfferedForCustomProfiles()
    {
        await LoadAsync();
        await _viewModel.CreateProfileAsync("Mine", null);

        Assert.False(_viewModel.ResetToBuiltIn(_viewModel.EditingProfile!.Id));
    }

    [Fact]
    public async Task Search_MatchesLabelFlagDescriptionAndSectionName()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");

        _viewModel.SearchText = "subtitle";
        var visible = _viewModel.OptionSections.Where(s => s.IsVisible).Select(s => s.Name).ToList();
        Assert.Contains("Subtitles", visible);

        _viewModel.SearchText = "-f";
        Assert.True(Option("-f").IsVisible);

        _viewModel.SearchText = "sort order";
        Assert.True(Option("-S").IsVisible);
        Assert.False(Option("-f").IsVisible);

        _viewModel.SearchText = "Container";
        Assert.True(Option("--merge-output-format").IsVisible);

        _viewModel.SearchText = "parallel";
        Assert.True(Option("--concurrent-fragments").IsVisible);

        _viewModel.SearchText = "sponsorblock";
        Assert.True(Option("--sponsorblock-remove").IsVisible);

        _viewModel.SearchText = "authentication";
        Assert.All(_viewModel.OptionSections.Single(s => s.Name == "Authentication").Options, o => Assert.True(o.IsVisible));
    }

    [Fact]
    public async Task Search_HidesNonMatchingSections_AutoExpandsMatches_AndClearingRestoresExpansion()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");
        var format = _viewModel.OptionSections.Single(s => s.Name == "Video Format");
        var subtitles = _viewModel.OptionSections.Single(s => s.Name == "Subtitles");
        Assert.True(format.IsExpanded);
        Assert.False(subtitles.IsExpanded);

        _viewModel.SearchText = "subs";

        Assert.False(format.IsVisible);
        Assert.True(subtitles.IsVisible);
        Assert.True(subtitles.IsExpanded);

        _viewModel.SearchText = string.Empty;

        Assert.True(format.IsVisible);
        Assert.True(format.IsExpanded);
        Assert.False(subtitles.IsExpanded);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_AndCountsMatches()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");

        _viewModel.SearchText = "EMBED";

        Assert.True(_viewModel.MatchCount >= 3);
        Assert.Equal($"{_viewModel.MatchCount} matches", _viewModel.MatchText);
        Assert.False(_viewModel.NoResults);
    }

    [Fact]
    public async Task ChangedOnly_HidesUnchangedRows_AndCombinesWithSearch()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");

        _viewModel.ShowChangedOnly = true;

        var visible = _viewModel.OptionSections.SelectMany(s => s.Options).Where(o => o.IsVisible).ToList();
        Assert.NotEmpty(visible);
        Assert.All(visible, o => Assert.True(o.IsChanged));
        Assert.DoesNotContain(_viewModel.OptionSections, s => s.IsVisible && s.ChangedCount == 0);

        _viewModel.SearchText = "container";
        Assert.Equal(["--merge-output-format"], _viewModel.OptionSections.SelectMany(s => s.Options).Where(o => o.IsVisible).Select(o => o.Flag));
    }

    [Fact]
    public async Task ChangedOnly_WithNoMatch_NamesTheUnchangedOptionAndOffersToShowAll()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");

        _viewModel.ShowChangedOnly = true;
        _viewModel.SearchText = "cookie";

        Assert.True(_viewModel.NoResults);
        Assert.Equal("No changed options match “cookie”", _viewModel.EmptyTitle);
        Assert.Equal("--cookies isn’t changed in this profile.", _viewModel.EmptyDetail);
        Assert.Equal("Show all options", _viewModel.EmptyActionText);
        Assert.Equal("No matches", _viewModel.MatchText);

        _viewModel.ShowChangedOnly = false;

        Assert.False(_viewModel.NoResults);
        Assert.True(Option("--cookies").IsVisible);
    }

    [Fact]
    public async Task Search_NoMatchAnywhere_OffersToClearTheSearch()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");

        _viewModel.SearchText = "zzzz-nothing";

        Assert.True(_viewModel.NoResults);
        Assert.Equal("No options match “zzzz-nothing”", _viewModel.EmptyTitle);
        Assert.Equal(string.Empty, _viewModel.EmptyDetail);
        Assert.Equal("Clear search", _viewModel.EmptyActionText);
    }

    [Fact]
    public async Task Search_AlsoMatchesTheExtraArguments()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        _viewModel.ExtraArgs = "--embed-chapters";

        _viewModel.SearchText = "chapters";

        Assert.True(_viewModel.CommandLineVisible);
        Assert.False(_viewModel.ShowCliPreview);
        Assert.False(_viewModel.NoResults);

        _viewModel.SearchText = "zzzz-nothing";
        Assert.False(_viewModel.CommandLineVisible);
    }

    [Fact]
    public async Task OpeningAProfile_ClearsTheFilter()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        _viewModel.SearchText = "x";
        _viewModel.ShowChangedOnly = true;

        _viewModel.CloseProfile();
        _viewModel.OpenProfile("hq-video");

        Assert.Equal(string.Empty, _viewModel.SearchText);
        Assert.False(_viewModel.ShowChangedOnly);
    }

    [Fact]
    public async Task CliPreview_FollowsTheOpenDraft()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");
        var before = _viewModel.CliPreview;

        Option("--proxy").StringValue = "socks5://x";

        Assert.NotEqual(before, _viewModel.CliPreview);
        Assert.Contains("--proxy socks5://x", _viewModel.CliPreview);
    }

    [Fact]
    public async Task CliPreview_WithHalfTypedExtraArguments_ExplainsInsteadOfThrowing()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");

        _viewModel.ExtraArgs = "--x \"open";

        Assert.StartsWith("Cannot build the command line", _viewModel.CliPreview);
    }

    [Fact]
    public async Task MarkAllSaved_ClearsDirtyState()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        _viewModel.ExtraArgs = "--no-mtime";

        _viewModel.MarkAllSaved();

        Assert.Equal(0, _viewModel.DirtyProfileCount);
        Assert.Empty(_viewModel.GetDirtyDrafts());
    }

    [Fact]
    public async Task ValidateDirty_PrefixesTheProfileName_AndSkipsSettingsErrors()
    {
        await LoadAsync();
        _viewModel.OpenProfile("default");
        _viewModel.ExtraArgs = "--x \"unterminated";

        var errors = _viewModel.ValidateDirty([]);

        var error = Assert.Single(errors);
        Assert.Equal("default", error.ProfileId);
        Assert.StartsWith("Default: ", error.Message);
        Assert.Empty(_viewModel.ValidateDirty([error.Message["Default: ".Length..]]));
    }

    [Fact]
    public async Task LoadAsync_StaysOnTheOpenProfile_WhenItStillExists()
    {
        await LoadAsync();
        _viewModel.OpenProfile("hq-video");
        _viewModel.ExtraArgs = "--no-mtime";

        await LoadAsync();

        Assert.Equal("hq-video", _viewModel.EditingProfile?.Id);
        Assert.Equal(string.Empty, _viewModel.ExtraArgs);
        Assert.Equal(0, _viewModel.DirtyProfileCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
