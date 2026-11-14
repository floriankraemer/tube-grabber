// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

public sealed class ChannelResultViewModel : ThumbnailRowViewModel
{
    private readonly ISubscriptionMonitor _monitor;
    private readonly IAppConfigStore _appConfigStore;
    private DownloadProfile? _selectedProfile;
    private bool _isSubscribing;
    private bool _isSubscribed;
    private string? _statusMessage;

    public ChannelResultViewModel(
        ChannelSearchResult result,
        IReadOnlyList<DownloadProfile> profiles,
        DownloadProfile? defaultProfile,
        bool isSubscribed,
        ISubscriptionMonitor monitor,
        IAppConfigStore appConfigStore,
        IThumbnailLoader thumbnailLoader)
        : base(result.ThumbnailUrl, thumbnailLoader)
    {
        Result = result;
        Profiles = profiles;
        _monitor = monitor;
        _appConfigStore = appConfigStore;
        _selectedProfile = defaultProfile ?? profiles.FirstOrDefault();
        _isSubscribed = isSubscribed;
        _statusMessage = isSubscribed ? "Subscribed." : null;
    }

    public ChannelSearchResult Result { get; }
    public string Name => Result.Name;

    public string Subtitle => string.Join(
        " · ",
        new[] { Result.Handle, SubscriberCountFormatter.Format(Result.SubscriberCount) }
            .Where(part => !string.IsNullOrEmpty(part)));

    public IReadOnlyList<DownloadProfile> Profiles { get; }

    public DownloadProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
                OnPropertyChanged(nameof(CanSubscribe));
        }
    }

    public bool IsSubscribed
    {
        get => _isSubscribed;
        private set
        {
            if (SetProperty(ref _isSubscribed, value))
                OnPropertyChanged(nameof(CanSubscribe));
        }
    }

    public bool IsSubscribing
    {
        get => _isSubscribing;
        private set
        {
            if (SetProperty(ref _isSubscribing, value))
                OnPropertyChanged(nameof(CanSubscribe));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool CanSubscribe => !IsSubscribing && !IsSubscribed && SelectedProfile is not null;

    public async Task SubscribeAsync()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = "Select a profile.";
            return;
        }

        IsSubscribing = true;
        StatusMessage = null;
        try
        {
            // Same "download latest N existing videos" setting as the Subscriptions page.
            var config = await _appConfigStore.LoadAsync();
            await _monitor.AddAsync(Result.ChannelUrl, SelectedProfile.Id, config.SubscriptionBacklogCount);
            IsSubscribed = true;
            StatusMessage = config.SubscriptionBacklogCount > 0
                ? $"Subscribed. Downloading the latest {config.SubscriptionBacklogCount} video(s)."
                : "Subscribed. New uploads will be downloaded automatically.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSubscribing = false;
        }
    }
}
