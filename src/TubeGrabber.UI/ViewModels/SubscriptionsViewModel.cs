// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

public sealed class SubscriptionsViewModel : ViewModelBase, IDisposable
{
    public const int MaxBacklogCount = 30;

    private readonly ISubscriptionMonitor _monitor;
    private readonly IProfileStore _profileStore;
    private readonly IAppConfigStore _appConfigStore;
    private readonly List<DownloadProfile> _profileSnapshot = [];

    private string _channelUrl = string.Empty;
    private DownloadProfile? _selectedProfile;
    private decimal _backlogCount = 3;
    private bool _isBusy;
    private bool _isChecking;
    private string? _statusMessage;
    private string? _errorMessage;

    public SubscriptionsViewModel(
        ISubscriptionMonitor monitor,
        IProfileStore profileStore,
        IAppConfigStore appConfigStore)
    {
        _monitor = monitor;
        _profileStore = profileStore;
        _appConfigStore = appConfigStore;
        Profiles = new ObservableCollection<DownloadProfile>();
        Items = new ObservableCollection<SubscriptionItemViewModel>();
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(CountText));
        };
        _monitor.SubscriptionsChanged += OnSubscriptionsChanged;
    }

    public ObservableCollection<DownloadProfile> Profiles { get; }
    public ObservableCollection<SubscriptionItemViewModel> Items { get; }

    public string ChannelUrl
    {
        get => _channelUrl;
        set => SetProperty(ref _channelUrl, value);
    }

    public DownloadProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public decimal BacklogCount
    {
        get => _backlogCount;
        set => SetProperty(ref _backlogCount, Math.Clamp(value, 0, MaxBacklogCount));
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            OnPropertyChanged(nameof(CanSubscribe));
            OnPropertyChanged(nameof(IsWorking));
        }
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (!SetProperty(ref _isChecking, value))
                return;

            OnPropertyChanged(nameof(CanCheckNow));
            OnPropertyChanged(nameof(IsWorking));
        }
    }

    public bool CanSubscribe => !IsBusy;
    public bool CanCheckNow => !IsChecking;
    public bool IsWorking => IsBusy || IsChecking;
    public bool HasItems => Items.Count > 0;

    public string CountText => Items.Count switch
    {
        0 => "Follow channels to download new uploads automatically.",
        1 => "1 channel, checked periodically.",
        var n => $"{n} channels, checked periodically.",
    };

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public Task InitializeAsync() => RefreshProfilesAsync();

    /// <summary>Reloads the profile pickers (for example after profiles were added or deleted in Settings) and the rows.</summary>
    public async Task RefreshProfilesAsync()
    {
        var (profiles, selected) = await ProfileListLoader.LoadWithSelectionAsync(_profileStore, _appConfigStore);
        _profileSnapshot.Clear();
        _profileSnapshot.AddRange(profiles);
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);

        SelectedProfile = selected;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var subscriptions = await _monitor.ListAsync();
            SyncItems(subscriptions);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsChecking = _monitor.IsChecking;
        }
    }

    public async Task SubscribeAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(ChannelUrl))
        {
            ErrorMessage = "Enter a channel URL.";
            return;
        }

        if (SelectedProfile is null)
        {
            ErrorMessage = "Select a profile.";
            return;
        }

        IsBusy = true;
        try
        {
            var backlog = (int)BacklogCount;
            var subscription = await _monitor.AddAsync(
                ChannelUrl.Trim(),
                SelectedProfile.Id,
                backlog);
            ChannelUrl = string.Empty;
            StatusMessage = backlog > 0
                ? $"Subscribed to {subscription.ChannelName}. Downloading the latest {backlog} video(s)."
                : $"Subscribed to {subscription.ChannelName}. New uploads will be downloaded automatically.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CheckNowAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsChecking = true;
        try
        {
            await _monitor.CheckNowAsync();
            StatusMessage = "Check finished.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            await RefreshAsync();
        }
    }

    public void Dispose() => _monitor.SubscriptionsChanged -= OnSubscriptionsChanged;

    private async Task RemoveItemAsync(SubscriptionItemViewModel item)
    {
        await _monitor.RemoveAsync(item.Id);
        await RefreshAsync();
    }

    private void SyncItems(IReadOnlyList<ChannelSubscription> subscriptions)
    {
        var ids = subscriptions.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!ids.Contains(Items[i].Id))
                Items.RemoveAt(i);
        }

        foreach (var subscription in subscriptions)
        {
            var existing = Items.FirstOrDefault(item => item.Id == subscription.Id);
            if (existing is null)
            {
                Items.Add(new SubscriptionItemViewModel(
                    subscription,
                    _profileSnapshot,
                    _monitor,
                    RemoveItemAsync,
                    message => ErrorMessage = message));
            }
            else
            {
                existing.Update(subscription);
            }
        }

    }

    private void OnSubscriptionsChanged(object? sender, EventArgs e)
    {
        // No Application (e.g. unit tests) means no UI thread to marshal to.
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            _ = RefreshAsync();
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(RefreshAsync);
    }
}
