// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.ViewModels;

/// <summary>One profile as shown in the list and in the detail header. Updated in place as the draft changes.</summary>
public sealed class ProfileCardViewModel : ViewModelBase
{
    public const string DownloadIcon = "download";
    public const string MusicIcon = "music";
    public const string FilmIcon = "film";

    private string _name = string.Empty;
    private string _summary = string.Empty;
    private string _headerSummary = string.Empty;
    private string _iconKey = FilmIcon;
    private bool _isDirty;
    private int _subscriptionCount;
    private int _changedCount;

    public ProfileCardViewModel(string id, bool isBuiltIn)
    {
        Id = id;
        IsBuiltIn = isBuiltIn;
    }

    public string Id { get; }
    public bool IsBuiltIn { get; }
    public bool IsCustom => !IsBuiltIn;

    public string Name
    {
        get => _name;
        private set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(AutomationName));
        }
    }

    public string Summary
    {
        get => _summary;
        private set
        {
            if (SetProperty(ref _summary, value))
                OnPropertyChanged(nameof(AutomationName));
        }
    }

    /// <summary>The summary without the option count, which the detail header shows on its own line.</summary>
    public string HeaderSummary
    {
        get => _headerSummary;
        private set => SetProperty(ref _headerSummary, value);
    }

    public string IconKey
    {
        get => _iconKey;
        private set
        {
            if (!SetProperty(ref _iconKey, value))
                return;

            OnPropertyChanged(nameof(IsDownloadIcon));
            OnPropertyChanged(nameof(IsMusicIcon));
            OnPropertyChanged(nameof(IsFilmIcon));
        }
    }

    public bool IsDownloadIcon => IconKey == DownloadIcon;
    public bool IsMusicIcon => IconKey == MusicIcon;
    public bool IsFilmIcon => IconKey == FilmIcon;

    /// <summary>True when the draft differs from what is saved on disk.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public int SubscriptionCount
    {
        get => _subscriptionCount;
        set
        {
            if (!SetProperty(ref _subscriptionCount, value))
                return;

            OnPropertyChanged(nameof(UsedByText));
            OnPropertyChanged(nameof(HasSubscriptions));
            OnPropertyChanged(nameof(DetailUsedByText));
        }
    }

    public bool HasSubscriptions => SubscriptionCount > 0;
    public string UsedByText => SubscriptionCount == 0 ? string.Empty : $"Used by {SubscriptionCount} {SubscriptionNoun(SubscriptionCount)}";
    public string DetailUsedByText => SubscriptionCount == 0 ? string.Empty : $"· {UsedByText}";

    /// <summary>Number of options that differ from the yt-dlp defaults.</summary>
    public int ChangedCount
    {
        get => _changedCount;
        private set
        {
            if (SetProperty(ref _changedCount, value))
                OnPropertyChanged(nameof(ChangedCountText));
        }
    }

    public string ChangedCountText => ChangedCount == 1 ? "1 option set" : $"{ChangedCount} options set";

    public string AutomationName => $"{Name}, {Summary}";
    public string MoreActionsName => $"More actions for {Name}";

    public void Update(string name, string summary, string headerSummary, string iconKey, bool isDirty, int changedCount)
    {
        Name = name;
        Summary = summary;
        HeaderSummary = headerSummary;
        IconKey = iconKey;
        IsDirty = isDirty;
        ChangedCount = changedCount;
        OnPropertyChanged(nameof(MoreActionsName));
    }

    public static string SubscriptionNoun(int count) => count == 1 ? "subscription" : "subscriptions";
}
