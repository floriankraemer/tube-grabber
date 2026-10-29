// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Globalization;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Models;

namespace YtDlpUi.UI.ViewModels;

public sealed class SubscriptionItemViewModel : ViewModelBase
{
    private readonly ISubscriptionMonitor _monitor;
    private readonly Func<SubscriptionItemViewModel, Task> _remove;
    private readonly Action<string?> _reportError;
    private ChannelSubscription _subscription;
    private DownloadProfile? _selectedProfile;
    private bool _enabled;
    private bool _syncing;

    public SubscriptionItemViewModel(
        ChannelSubscription subscription,
        IReadOnlyList<DownloadProfile> profiles,
        ISubscriptionMonitor monitor,
        Func<SubscriptionItemViewModel, Task> remove,
        Action<string?> reportError)
    {
        _subscription = subscription;
        _monitor = monitor;
        _remove = remove;
        _reportError = reportError;
        Profiles = profiles;
        Apply(subscription);
    }

    public IReadOnlyList<DownloadProfile> Profiles { get; }

    public string Id => _subscription.Id;
    public string ChannelName => _subscription.ChannelName;
    public string ChannelUrl => _subscription.ChannelUrl;
    public string? LastError => _subscription.LastError;
    public bool HasError => !string.IsNullOrEmpty(_subscription.LastError);

    public string LastCheckedDisplay => _subscription.LastCheckedAt is { } at
        ? $"Last checked {at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}"
        : "Never checked";

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetProperty(ref _enabled, value) || _syncing)
                return;

            _ = RunAsync(() => _monitor.SetEnabledAsync(Id, value));
        }
    }

    public DownloadProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value) || _syncing || value is null)
                return;

            _ = RunAsync(() => _monitor.SetProfileAsync(Id, value.Id));
        }
    }

    public Task RemoveAsync() => RunAsync(() => _remove(this));

    public void Update(ChannelSubscription subscription)
    {
        _subscription = subscription;
        Apply(subscription);
        OnPropertyChanged(string.Empty);
    }

    private void Apply(ChannelSubscription subscription)
    {
        _syncing = true;
        try
        {
            Enabled = subscription.Enabled;
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == subscription.ProfileId);
        }
        finally
        {
            _syncing = false;
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            _reportError(null);
            await action();
        }
        catch (Exception ex)
        {
            _reportError(ex.Message);
        }
    }
}
