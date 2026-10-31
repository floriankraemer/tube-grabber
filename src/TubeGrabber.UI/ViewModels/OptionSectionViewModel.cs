// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;

namespace TubeGrabber.UI.ViewModels;

/// <summary>One expander of the profile detail page: a catalog section with its option rows.</summary>
public sealed class OptionSectionViewModel : ViewModelBase
{
    private bool _userExpanded;
    private bool _isExpanded;
    private bool _isVisible = true;
    private int _changedCount;
    private int _visibleCount;

    public OptionSectionViewModel(string name, IReadOnlyList<OptionItemViewModel> options)
    {
        Name = name;
        Options = new ObservableCollection<OptionItemViewModel>(options);
        _visibleCount = options.Count;
        _changedCount = options.Count(option => option.IsChanged);

        // Sections with changes start open so the page shows what makes the profile special.
        _userExpanded = _isExpanded = _changedCount > 0;
        foreach (var option in options)
            option.PropertyChanged += OnOptionPropertyChanged;
    }

    public string Name { get; }
    public ObservableCollection<OptionItemViewModel> Options { get; }

    /// <summary>Raised when an option of this section flips between default and changed.</summary>
    public event EventHandler? ChangedCountChanged;

    public int ChangedCount
    {
        get => _changedCount;
        private set
        {
            if (!SetProperty(ref _changedCount, value))
                return;

            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(ChangedText));
            OnPropertyChanged(nameof(ShowCount));
            ChangedCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasChanges => ChangedCount > 0;
    public bool ShowCount => ChangedCount == 0;
    public string ChangedText => $"{ChangedCount} changed";

    /// <summary>Options passing the current filter.</summary>
    public int VisibleCount
    {
        get => _visibleCount;
        private set
        {
            if (SetProperty(ref _visibleCount, value))
                OnPropertyChanged(nameof(CountText));
        }
    }

    public string CountText => VisibleCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// Effective expansion. While a filter is active, sections with matches open on their own; clearing the filter
    /// restores what the user had open.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
                return;

            if (!IsFiltering)
                _userExpanded = value;
        }
    }

    public bool IsFiltering { get; private set; }

    public void ApplyFilter(string query, bool changedOnly)
    {
        query = query.Trim();
        IsFiltering = query.Length > 0 || changedOnly;
        var sectionMatches = query.Length > 0 && Name.Contains(query, StringComparison.OrdinalIgnoreCase);

        foreach (var option in Options)
        {
            option.IsVisible = (!changedOnly || option.IsChanged)
                && (query.Length == 0 || sectionMatches || option.Matches(query));
        }

        VisibleCount = Options.Count(option => option.IsVisible);
        IsVisible = VisibleCount > 0;
        IsExpanded = IsFiltering ? VisibleCount > 0 : _userExpanded;
    }

    private void OnOptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OptionItemViewModel.IsChanged))
            ChangedCount = Options.Count(option => option.IsChanged);
    }
}
