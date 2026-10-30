// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;
using YtDlpUi.UI.Services;

namespace YtDlpUi.UI.ViewModels;

public sealed class SearchResultViewModel : ThumbnailRowViewModel
{
    private readonly DownloadEnqueueCoordinator _enqueueCoordinator;
    private DownloadProfile? _selectedProfile;
    private bool _isAdding;
    private string? _statusMessage;

    public SearchResultViewModel(
        YouTubeSearchResult result,
        IReadOnlyList<DownloadProfile> profiles,
        DownloadProfile? defaultProfile,
        DownloadEnqueueCoordinator enqueueCoordinator,
        IThumbnailLoader thumbnailLoader)
        : base(result.ThumbnailUrl, thumbnailLoader)
    {
        Result = result;
        Title = result.Title;
        Channel = result.Channel ?? string.Empty;
        WatchUrl = result.WatchUrl;
        DurationDisplay = DurationFormatter.Format(result.DurationSeconds);
        Profiles = profiles;
        _enqueueCoordinator = enqueueCoordinator;
        _selectedProfile = defaultProfile ?? profiles.FirstOrDefault();
    }

    public YouTubeSearchResult Result { get; }
    public string Title { get; }
    public string Channel { get; }
    public string WatchUrl { get; }
    public string DurationDisplay { get; }
    public IReadOnlyList<DownloadProfile> Profiles { get; }

    public DownloadProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool IsAdding
    {
        get => _isAdding;
        private set => SetProperty(ref _isAdding, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool CanAdd => !IsAdding && SelectedProfile is not null;

    public async Task AddToQueueAsync()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = "Select a profile.";
            return;
        }

        IsAdding = true;
        OnPropertyChanged(nameof(CanAdd));
        StatusMessage = null;

        try
        {
            var result = await _enqueueCoordinator.TryEnqueueAsync(WatchUrl, SelectedProfile.Id);
            StatusMessage = result.IsSuccess ? "Added to queue." : result.Error;
        }
        finally
        {
            IsAdding = false;
            OnPropertyChanged(nameof(CanAdd));
        }
    }
}
