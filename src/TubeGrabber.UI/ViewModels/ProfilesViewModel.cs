// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using System.ComponentModel;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.UI.ViewModels;

/// <summary>
/// The Profiles tab of Settings: a list of profiles and, once one is opened, its detail page.
/// Every profile is held as a draft next to a snapshot of what is on disk; a profile is dirty while the two differ.
/// </summary>
public sealed class ProfilesViewModel : ViewModelBase
{
    public const string DefaultNewProfileName = "New profile";

    private readonly SettingsCoordinator _coordinator;
    private readonly YtDlpOptionCatalog _catalog;
    private readonly IProfileStore _profileStore;
    private readonly ISubscriptionMonitor _subscriptionMonitor;

    private readonly Dictionary<string, DownloadProfile> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DownloadProfile> _saved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProfileCardViewModel> _cardsById = new(StringComparer.Ordinal);
    private Dictionary<string, int> _subscriptionCounts = new(StringComparer.Ordinal);

    private AppConfiguration _config = new();
    private ProfileCardViewModel? _editingProfile;
    private string _lastViewedId = AppPaths.DefaultProfileId;
    private string _searchText = string.Empty;
    private bool _showChangedOnly;
    private string _cliPreview = string.Empty;
    private bool _commandLineVisible = true;
    private int _matchCount;

    public ProfilesViewModel(
        SettingsCoordinator coordinator,
        YtDlpOptionCatalog catalog,
        IProfileStore profileStore,
        ISubscriptionMonitor subscriptionMonitor)
    {
        _coordinator = coordinator;
        _catalog = catalog;
        _profileStore = profileStore;
        _subscriptionMonitor = subscriptionMonitor;
        Cards = new ObservableCollection<ProfileCardViewModel>();
        OptionSections = new ObservableCollection<OptionSectionViewModel>();
    }

    /// <summary>Raised when the set of unsaved profiles may have changed.</summary>
    public event EventHandler? DirtyStateChanged;

    /// <summary>Raised after a profile was created, duplicated or deleted on disk, so other pages can refresh their pickers.</summary>
    public event EventHandler? ProfilesChanged;

    /// <summary>Raised with a user-readable message when a profile operation failed.</summary>
    public event EventHandler<string>? OperationFailed;

    public ObservableCollection<ProfileCardViewModel> Cards { get; }
    public ObservableCollection<OptionSectionViewModel> OptionSections { get; }

    /// <summary>The profile whose detail page is open; null shows the list.</summary>
    public ProfileCardViewModel? EditingProfile
    {
        get => _editingProfile;
        private set
        {
            if (!SetProperty(ref _editingProfile, value))
                return;

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsListMode));
            OnPropertyChanged(nameof(IsEditingBuiltIn));
            OnPropertyChanged(nameof(CanRename));
            OnPropertyChanged(nameof(RenameTip));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(ExtraArgs));
        }
    }

    public bool IsEditing => EditingProfile is not null;
    public bool IsListMode => EditingProfile is null;
    public bool IsEditingBuiltIn => EditingProfile is { IsBuiltIn: true };

    /// <summary>Built-in names are locked; they are how the app finds its own profiles again.</summary>
    public bool CanRename => EditingProfile is { IsBuiltIn: false };

    public string RenameTip => CanRename
        ? "Rename (F2)"
        : "Built-in profiles can’t be renamed. Duplicate it to make your own variant.";

    public bool CanDelete => EditingProfile is not null && CanDeleteProfile(EditingProfile.Id);

    public int DirtyProfileCount => _cardsById.Values.Count(card => card.IsDirty);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public bool ShowChangedOnly
    {
        get => _showChangedOnly;
        set
        {
            if (SetProperty(ref _showChangedOnly, value))
                ApplyFilter();
        }
    }

    public bool HasQuery => SearchText.Trim().Length > 0;
    public bool IsFiltering => HasQuery || ShowChangedOnly;

    /// <summary>Options (plus the extra-arguments box) passing the current filter.</summary>
    public int MatchCount
    {
        get => _matchCount;
        private set
        {
            if (!SetProperty(ref _matchCount, value))
                return;

            OnPropertyChanged(nameof(MatchText));
            OnPropertyChanged(nameof(NoResults));
        }
    }

    public string MatchText => MatchCount switch
    {
        0 => "No matches",
        1 => "1 match",
        _ => $"{MatchCount} matches",
    };

    public bool NoResults => IsFiltering && MatchCount == 0;

    public string EmptyTitle
    {
        get
        {
            var query = SearchText.Trim();
            if (query.Length == 0)
                return "No options differ from the defaults";

            return ShowChangedOnly ? $"No changed options match “{query}”" : $"No options match “{query}”";
        }
    }

    /// <summary>When "changed only" hides an option that does match the search, say so by name.</summary>
    public string EmptyDetail
    {
        get
        {
            var query = SearchText.Trim();
            if (!ShowChangedOnly || query.Length == 0)
                return string.Empty;

            var hidden = OptionSections.SelectMany(section => section.Options).FirstOrDefault(option => option.Matches(query));
            return hidden is null ? string.Empty : $"{hidden.Flag} isn’t changed in this profile.";
        }
    }

    public bool HasEmptyDetail => EmptyDetail.Length > 0;
    public string EmptyActionText => ShowChangedOnly ? "Show all options" : "Clear search";

    /// <summary>Number of changed options in the open profile; shown as the badge on the "Changed only" toggle.</summary>
    public int ChangedCount => EditingProfile?.ChangedCount ?? 0;

    public bool CommandLineVisible
    {
        get => _commandLineVisible;
        private set => SetProperty(ref _commandLineVisible, value);
    }

    /// <summary>The preview is long; while filtering the command-line group shrinks to the arguments box.</summary>
    public bool ShowCliPreview => !IsFiltering;

    public string CliPreview
    {
        get => _cliPreview;
        private set => SetProperty(ref _cliPreview, value);
    }

    public string ExtraArgs
    {
        get => EditingDraft?.ExtraArgs ?? string.Empty;
        set
        {
            if (EditingDraft is not { } draft || draft.ExtraArgs == value)
                return;

            draft.ExtraArgs = value ?? string.Empty;
            OnPropertyChanged();
            OnDraftChanged(draft.Id);
            ApplyFilter();
        }
    }

    public string UnsavedSummary => DirtyProfileCount switch
    {
        0 => string.Empty,
        1 => "Unsaved changes in 1 profile",
        var n => $"Unsaved changes in {n} profiles",
    };

    private DownloadProfile? EditingDraft => EditingProfile is { } card ? _drafts.GetValueOrDefault(card.Id) : null;

    /// <summary>Replaces all drafts with the profiles on disk. Stays on the open profile when it still exists.</summary>
    public async Task LoadAsync(AppConfiguration config, IReadOnlyList<DownloadProfile> profiles)
    {
        _config = config;
        var reopen = EditingProfile?.Id;
        _drafts.Clear();
        _saved.Clear();
        foreach (var profile in profiles)
        {
            _saved[profile.Id] = ProfileOptionValues.Clone(profile);
            _drafts[profile.Id] = ProfileOptionValues.Clone(profile);
        }

        await RefreshSubscriptionCountsAsync();
        SyncCards();
        if (reopen is not null && _drafts.ContainsKey(reopen))
            OpenProfile(reopen);
        else
            CloseProfile();

        RaiseDirtyStateChanged();
    }

    /// <summary>Recomputes the command-line preview, e.g. after a binary path changed.</summary>
    public void RefreshPreview()
    {
        if (EditingDraft is not { } draft)
        {
            CliPreview = string.Empty;
            return;
        }

        try
        {
            CliPreview = _coordinator.BuildCliPreview(_config, draft);
        }
        catch (InvalidOperationException ex)
        {
            // Half-typed extra arguments (an open quote, say) cannot be tokenized; the preview says so instead of crashing.
            CliPreview = $"Cannot build the command line: {ex.Message}";
        }
    }

    public void OpenProfile(string id)
    {
        if (!_drafts.TryGetValue(id, out var draft))
            return;

        _lastViewedId = id;
        SearchText = string.Empty;
        ShowChangedOnly = false;
        BuildSections(draft);
        EditingProfile = _cardsById[id];
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(ExtraArgs));
        RefreshPreview();
        ApplyFilter();
    }

    /// <summary>Back to the list. Drafts are kept, so unsaved edits show up as "Unsaved" pills.</summary>
    public void CloseProfile()
    {
        EditingProfile = null;
        OptionSections.Clear();
        CliPreview = string.Empty;
        SearchText = string.Empty;
        ShowChangedOnly = false;
    }

    public bool CanDeleteProfile(string id) =>
        !BuiltInProfiles.IsBuiltIn(id) && _drafts.ContainsKey(id) && _drafts.Count > 1;

    public string? NameError(string name, string? exceptId = null)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return "Enter a name for the profile.";

        var taken = _drafts.Values.Any(profile =>
            profile.Id != exceptId && string.Equals(profile.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        return taken ? "A profile with this name already exists. Choose a different name." : null;
    }

    public bool IsNameAvailable(string name, string? exceptId = null) => NameError(name, exceptId) is null;

    /// <summary>The first free name of the form "base", "base 2", "base 3"...</summary>
    public string UniqueName(string baseName, string separator = " ")
    {
        if (IsNameAvailable(baseName))
            return baseName;

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName}{separator}{n}";
            if (IsNameAvailable(candidate))
                return candidate;
        }
    }

    public string SuggestNewName() => UniqueName(DefaultNewProfileName);

    /// <summary>"{Name} (copy)", then "{Name} (copy 2)"...</summary>
    public string SuggestCopyName(string sourceName)
    {
        var first = $"{sourceName} (copy)";
        if (IsNameAvailable(first))
            return first;

        for (var n = 2; ; n++)
        {
            var candidate = $"{sourceName} (copy {n})";
            if (IsNameAvailable(candidate))
                return candidate;
        }
    }

    /// <summary>Profiles offered as the starting point of a new one; the default is the profile viewed last.</summary>
    public string DefaultSourceId => _drafts.ContainsKey(_lastViewedId) ? _lastViewedId : AppPaths.DefaultProfileId;

    /// <summary>Creates a profile on disk right away, from the draft of <paramref name="sourceId"/> or empty, and opens it.</summary>
    public async Task<bool> CreateProfileAsync(string name, string? sourceId)
    {
        var source = sourceId is not null ? _drafts.GetValueOrDefault(sourceId) : null;
        var profile = new DownloadProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Options = source is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(source.Options, StringComparer.Ordinal),
            ExtraArgs = source?.ExtraArgs ?? string.Empty,
        };
        return await AddNewProfileAsync(profile);
    }

    /// <summary>Copies the draft (not the saved file) under a free "(copy)" name, saves it and opens it.</summary>
    public async Task<bool> DuplicateProfileAsync(string id)
    {
        if (!_drafts.TryGetValue(id, out var source))
            return false;

        var copy = ProfileOptionValues.Clone(source);
        var profile = new DownloadProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = SuggestCopyName(source.Name),
            Options = copy.Options,
            ExtraArgs = copy.ExtraArgs,
        };
        return await AddNewProfileAsync(profile);
    }

    /// <summary>Changes the name of the draft; it reaches disk with the next save. Built-in names stay as they are.</summary>
    public bool Rename(string id, string name)
    {
        if (!_drafts.TryGetValue(id, out var draft) || BuiltInProfiles.IsBuiltIn(id) || NameError(name, id) is not null)
            return false;

        draft.Name = name.Trim();
        OnDraftChanged(id);
        return true;
    }

    /// <summary>
    /// Deletes a custom profile: subscriptions that use it switch to Default, the file is removed and the page returns
    /// to the list. Any failure is reported through <see cref="OperationFailed"/> and leaves the profile in place.
    /// </summary>
    public async Task<bool> DeleteProfileAsync(string id)
    {
        if (!CanDeleteProfile(id))
            return false;

        try
        {
            foreach (var subscription in (await _subscriptionMonitor.ListAsync()).Where(s => s.ProfileId == id))
                await _subscriptionMonitor.SetProfileAsync(subscription.Id, AppPaths.DefaultProfileId);

            await _coordinator.DeleteProfileAsync(id);
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"Could not delete the profile: {ex.Message}");
            return false;
        }

        _drafts.Remove(id);
        _saved.Remove(id);
        if (_lastViewedId == id)
            _lastViewedId = AppPaths.DefaultProfileId;

        await RefreshSubscriptionCountsAsync();
        SyncCards();
        CloseProfile();
        RaiseDirtyStateChanged();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Puts a built-in profile back to its shipped options and arguments. The name stays; the profile becomes dirty.</summary>
    public bool ResetToBuiltIn(string id)
    {
        if (BuiltInProfiles.FindTemplate(id) is not { } template || !_drafts.TryGetValue(id, out var draft))
            return false;

        var fresh = ProfileOptionValues.Clone(template);
        draft.Options = fresh.Options;
        draft.ExtraArgs = fresh.ExtraArgs;
        if (EditingProfile?.Id == id)
        {
            BuildSections(draft);
            OnPropertyChanged(nameof(ExtraArgs));
        }

        OnDraftChanged(id);
        ApplyFilter();
        return true;
    }

    public IReadOnlyList<DownloadProfile> GetDirtyDrafts() =>
        _drafts.Values.Where(draft => IsDirty(draft.Id)).ToList();

    /// <summary>Validation errors of unsaved profiles, prefixed with the profile name. Errors of the settings themselves are skipped.</summary>
    public IReadOnlyList<(string ProfileId, string Message)> ValidateDirty(IReadOnlyCollection<string> settingsErrors)
    {
        var errors = new List<(string, string)>();
        foreach (var draft in GetDirtyDrafts())
        {
            foreach (var error in _coordinator.Validate(_config, draft).Where(e => !settingsErrors.Contains(e)))
                errors.Add((draft.Id, $"{(string.IsNullOrWhiteSpace(draft.Name) ? "Unnamed profile" : draft.Name)}: {error}"));

            if (!string.IsNullOrWhiteSpace(draft.Name) && NameError(draft.Name, draft.Id) is not null)
                errors.Add((draft.Id, $"{draft.Name}: Another profile has the same name."));
        }

        return errors;
    }

    /// <summary>Takes a snapshot of every draft after a successful save.</summary>
    public void MarkAllSaved()
    {
        foreach (var (id, draft) in _drafts)
            _saved[id] = ProfileOptionValues.Clone(draft);

        foreach (var id in _drafts.Keys)
            RefreshCard(id);

        RaiseDirtyStateChanged();
    }

    private bool IsDirty(string id) =>
        _saved.TryGetValue(id, out var saved) && _drafts.TryGetValue(id, out var draft)
        && !ProfileOptionValues.AreEquivalent(draft, saved, _catalog);

    private async Task<bool> AddNewProfileAsync(DownloadProfile profile)
    {
        if (NameError(profile.Name) is { } nameError)
        {
            OperationFailed?.Invoke(this, nameError);
            return false;
        }

        try
        {
            await _profileStore.SaveAsync(profile);
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"Could not create the profile: {ex.Message}");
            return false;
        }

        _drafts[profile.Id] = profile;
        _saved[profile.Id] = ProfileOptionValues.Clone(profile);
        SyncCards();
        OpenProfile(profile.Id);
        RaiseDirtyStateChanged();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private async Task RefreshSubscriptionCountsAsync()
    {
        IReadOnlyList<ChannelSubscription> subscriptions;
        try
        {
            subscriptions = await _subscriptionMonitor.ListAsync();
        }
        catch (Exception)
        {
            // The counts are informational; a corrupt subscriptions file must not block editing profiles.
            subscriptions = [];
        }

        _subscriptionCounts = subscriptions.GroupBy(s => s.ProfileId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var (id, card) in _cardsById)
            card.SubscriptionCount = _subscriptionCounts.GetValueOrDefault(id);
    }

    private void SyncCards()
    {
        foreach (var id in _cardsById.Keys.Where(id => !_drafts.ContainsKey(id)).ToList())
        {
            Cards.Remove(_cardsById[id]);
            _cardsById.Remove(id);
        }

        foreach (var id in _drafts.Keys)
        {
            if (!_cardsById.TryGetValue(id, out var card))
            {
                _cardsById[id] = card = new ProfileCardViewModel(id, BuiltInProfiles.IsBuiltIn(id));
                Cards.Add(card);
            }

            card.SubscriptionCount = _subscriptionCounts.GetValueOrDefault(id);
            RefreshCard(id);
        }

        SortCards();
    }

    private void SortCards()
    {
        var ordered = Cards.OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = Cards.IndexOf(ordered[i]);
            if (current != i)
                Cards.Move(current, i);
        }
    }

    private void RefreshCard(string id)
    {
        if (!_drafts.TryGetValue(id, out var draft) || !_cardsById.TryGetValue(id, out var card))
            return;

        var icon = id == AppPaths.DefaultProfileId ? ProfileCardViewModel.DownloadIcon
            : ProfileSummaryBuilder.IsAudioOnly(draft) ? ProfileCardViewModel.MusicIcon
            : ProfileCardViewModel.FilmIcon;
        card.Update(
            draft.Name,
            ProfileSummaryBuilder.Build(draft, _catalog),
            ProfileSummaryBuilder.Build(draft, _catalog, includeCount: false),
            icon,
            IsDirty(id),
            ProfileOptionValues.CountChanged(draft, _catalog));
    }

    /// <summary>Called whenever a draft was edited: refreshes the card, the preview and the unsaved count.</summary>
    private void OnDraftChanged(string id)
    {
        RefreshCard(id);
        if (EditingProfile?.Id == id)
        {
            RefreshPreview();
            OnPropertyChanged(nameof(ChangedCount));
        }

        RaiseDirtyStateChanged();
    }

    private void RaiseDirtyStateChanged()
    {
        OnPropertyChanged(nameof(DirtyProfileCount));
        OnPropertyChanged(nameof(UnsavedSummary));
        OnPropertyChanged(nameof(CanDelete));
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildSections(DownloadProfile draft)
    {
        OptionSections.Clear();

        // Sections with changes come first, most changed first, so the page opens on what makes the profile different.
        var sections = _catalog.GetSections()
            .OrderByDescending(name => _catalog.GetBySection(name).Count(def => ProfileOptionValues.IsChanged(def, draft.Options.GetValueOrDefault(def.Flag))))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            var items = _catalog.GetBySection(section)
                .Select(definition =>
                {
                    draft.Options.TryGetValue(definition.Flag, out var value);
                    var item = new OptionItemViewModel(definition, value);
                    item.PropertyChanged += (_, e) => OnOptionPropertyChanged(draft, definition.Flag, item, e);
                    return item;
                })
                .ToList();

            OptionSections.Add(new OptionSectionViewModel(section, items));
        }
    }

    private void OnOptionPropertyChanged(DownloadProfile draft, string flag, OptionItemViewModel item, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OptionItemViewModel.Value))
            return;

        draft.Options[flag] = item.Value;
        OnDraftChanged(draft.Id);
    }

    /// <summary>Applies the search text and the "changed only" toggle to the sections and the command-line group.</summary>
    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        foreach (var section in OptionSections)
            section.ApplyFilter(query, ShowChangedOnly);

        var extraArgs = ExtraArgs;
        var matchesExtra = (query.Length == 0
                || extraArgs.Contains(query, StringComparison.OrdinalIgnoreCase)
                || "extra arguments".Contains(query, StringComparison.OrdinalIgnoreCase)
                || "command line".Contains(query, StringComparison.OrdinalIgnoreCase))
            && (!ShowChangedOnly || !string.IsNullOrWhiteSpace(extraArgs));
        CommandLineVisible = !IsFiltering || matchesExtra;

        MatchCount = OptionSections.Sum(section => section.VisibleCount) + (IsFiltering && CommandLineVisible ? 1 : 0);
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(IsFiltering));
        OnPropertyChanged(nameof(ShowCliPreview));
        OnPropertyChanged(nameof(NoResults));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDetail));
        OnPropertyChanged(nameof(HasEmptyDetail));
        OnPropertyChanged(nameof(EmptyActionText));
    }
}
