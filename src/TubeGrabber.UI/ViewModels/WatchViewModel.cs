// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

/// <summary>
/// One opened video: resolves stream details and starts playback, loads comments and SponsorBlock segments in
/// parallel, and offers the profile-picking Download action. Disposing cancels everything and stops the player.
/// </summary>
public sealed class WatchViewModel : ViewModelBase, IDisposable
{
    private readonly YouTubeSearchResult _result;
    private readonly IVideoDetailsService _detailsService;
    private readonly IVideoCommentsService _commentsService;
    private readonly ISponsorBlockClient _sponsorBlock;
    private readonly DownloadEnqueueCoordinator _enqueueCoordinator;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly IVideoPlayer? _player;
    private readonly HashSet<string> _skipped = new(StringComparer.Ordinal);
    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _noticeCts;
    private CancellationTokenSource? _commentsCts;
    private CancellationTokenSource? _profilesCts;
    private IReadOnlyList<SponsorSegment> _segments = Array.Empty<SponsorSegment>();
    private VideoDetails? _details;
    private string? _detailsError;
    private string? _playbackError;
    private bool _isLoadingDetails = true;
    private bool _isPlaying;
    private bool _isScrubbing;
    private double _lengthSeconds;
    private double _seekValue;
    private double _positionSeconds;
    private int _volume = 100;
    private bool _isDescriptionExpanded;
    private bool _isFullscreen;
    private bool _isLoadingComments = true;
    private string? _commentsError;
    private string? _skipNotice;
    private bool _isDownloading;
    private string? _downloadMessage;
    private bool _downloadSucceeded;
    private bool _disposed;

    public WatchViewModel(
        YouTubeSearchResult result,
        IVideoDetailsService detailsService,
        IVideoCommentsService commentsService,
        ISponsorBlockClient sponsorBlock,
        DownloadEnqueueCoordinator enqueueCoordinator,
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        IVideoPlayer? player)
    {
        _result = result;
        _detailsService = detailsService;
        _commentsService = commentsService;
        _sponsorBlock = sponsorBlock;
        _enqueueCoordinator = enqueueCoordinator;
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _player = player;
        _lengthSeconds = result.DurationSeconds ?? 0;

        if (player is not null)
        {
            player.PositionChanged += OnPositionChanged;
            player.LengthChanged += OnLengthChanged;
            player.IsPlayingChanged += OnIsPlayingChanged;
            player.Failed += OnPlayerFailed;
        }
    }

    /// <summary>yt-dlp can hang on a bad network or extractor; the page then shows an error with Retry.</summary>
    public TimeSpan DetailsTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Comment extraction pages through the API and is slower than the details lookup.</summary>
    public TimeSpan CommentsTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>How long the "Skipped sponsor segment" notice stays visible.</summary>
    public TimeSpan SkipNoticeDuration { get; set; } = TimeSpan.FromSeconds(3);

    public IVideoPlayer? Player => _player;
    public bool IsPlaybackAvailable => _player is not null;
    public string WatchUrl => _result.WatchUrl;
    public ObservableCollection<CommentViewModel> Comments { get; } = new();
    public ObservableCollection<DownloadProfile> Profiles { get; } = new();
    public IReadOnlyList<SponsorSegment> Segments => _segments;

    public string Title => _details?.Title ?? _result.Title;
    public string Channel => _details?.Channel ?? _result.Channel ?? string.Empty;

    public string StatsLine
    {
        get
        {
            var parts = new List<string>();
            if (_details?.ViewCount is { } views)
                parts.Add($"{views:N0} views");
            if (_details?.UploadDate is { } date)
                parts.Add(date.ToString("MMM d, yyyy"));
            return string.Join(" · ", parts);
        }
    }

    public string Description => _details?.Description ?? string.Empty;
    public bool HasDescription => Description.Length > 0;

    // Collapsed text is clamped to a few lines, so only long or multi-line descriptions need the toggle.
    public bool CanExpandDescription => Description.Length > 200 || Description.Count(c => c == '\n') >= 3;

    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        private set
        {
            if (SetProperty(ref _isDescriptionExpanded, value))
                OnPropertyChanged(nameof(DescriptionToggleText));
        }
    }

    public string DescriptionToggleText => IsDescriptionExpanded ? "Show less" : "Show more";

    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        private set
        {
            if (SetProperty(ref _isLoadingDetails, value))
                OnPropertyChanged(nameof(ShowPlaybackSpinner));
        }
    }

    public string? DetailsError
    {
        get => _detailsError;
        private set
        {
            if (SetProperty(ref _detailsError, value))
                OnPropertyChanged(nameof(HasDetailsError));
        }
    }

    public bool HasDetailsError => _detailsError is not null;

    public string? PlaybackError
    {
        get => _playbackError;
        private set
        {
            if (SetProperty(ref _playbackError, value))
                OnPropertyChanged(nameof(HasPlaybackError));
        }
    }

    public bool HasPlaybackError => _playbackError is not null;

    public bool ShowPlaybackSpinner => IsLoadingDetails && IsPlaybackAvailable;

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetProperty(ref _isFullscreen, value);
    }

    public double LengthSeconds
    {
        get => _lengthSeconds;
        private set
        {
            if (!SetProperty(ref _lengthSeconds, value))
                return;

            OnPropertyChanged(nameof(CanSeek));
            OnPropertyChanged(nameof(TimeText));
        }
    }

    public bool CanSeek => _lengthSeconds > 0;

    /// <summary>Bound two-way to the seek slider. Playback ticks do not overwrite it while the user scrubs.</summary>
    public double SeekValue
    {
        get => _seekValue;
        set => SetProperty(ref _seekValue, value);
    }

    public string TimeText =>
        $"{Format(_positionSeconds)} / {(_lengthSeconds > 0 ? Format(_lengthSeconds) : "--:--")}";

    public int Volume
    {
        get => _volume;
        set
        {
            if (!SetProperty(ref _volume, Math.Clamp(value, 0, 100)) || _player is null)
                return;

            _player.Volume = _volume;
        }
    }

    public string? SkipNotice
    {
        get => _skipNotice;
        private set
        {
            if (SetProperty(ref _skipNotice, value))
                OnPropertyChanged(nameof(HasSkipNotice));
        }
    }

    public bool HasSkipNotice => _skipNotice is not null;

    public bool IsLoadingComments
    {
        get => _isLoadingComments;
        private set
        {
            if (SetProperty(ref _isLoadingComments, value))
                RaiseCommentStateChanged();
        }
    }

    public string? CommentsError
    {
        get => _commentsError;
        private set
        {
            if (SetProperty(ref _commentsError, value))
                RaiseCommentStateChanged();
        }
    }

    public bool HasCommentsError => _commentsError is not null;
    public bool HasComments => Comments.Count > 0;
    public bool ShowNoComments => !IsLoadingComments && !HasCommentsError && Comments.Count == 0;
    public string CommentsHeader => Comments.Count > 0 ? $"Comments ({Comments.Count})" : "Comments";

    public bool IsDownloading
    {
        get => _isDownloading;
        private set => SetProperty(ref _isDownloading, value);
    }

    public string? DownloadMessage
    {
        get => _downloadMessage;
        private set
        {
            if (SetProperty(ref _downloadMessage, value))
                OnPropertyChanged(nameof(HasDownloadMessage));
        }
    }

    public bool HasDownloadMessage => _downloadMessage is not null;

    public bool DownloadSucceeded
    {
        get => _downloadSucceeded;
        private set => SetProperty(ref _downloadSucceeded, value);
    }

    /// <summary>Loads everything; safe to call again as a retry after a details error.</summary>
    public async Task LoadAsync()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        DetailsError = null;
        PlaybackError = null;
        IsLoadingDetails = true;
        _skipped.Clear();

        await Task.WhenAll(
            LoadDetailsAndPlayAsync(token),
            LoadCommentsAsync(token),
            LoadSegmentsAsync(token),
            LoadProfilesAsync(token));
    }

    public Task RetryCommentsAsync() => LoadCommentsAsync(_cts.Token);

    // One in-flight load per kind: starting a new one cancels the previous, so results never interleave.
    private static CancellationToken BeginOperation(ref CancellationTokenSource? slot, CancellationToken pageToken)
    {
        slot?.Cancel();
        slot?.Dispose();
        slot = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
        return slot.Token;
    }

    private static async Task<T> RunWithTimeoutAsync<T>(
        TimeSpan timeout,
        string description,
        CancellationToken token,
        Func<CancellationToken, Task<T>> work)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await work(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"{description} took longer than {timeout.TotalSeconds:0} seconds. Check your connection and try again.");
        }
    }

    public void TogglePlayPause()
    {
        if (_player is null)
            return;

        if (IsPlaying)
            _player.Pause();
        else
            _player.Resume();
    }

    public void BeginScrub() => _isScrubbing = true;

    public void CommitSeek()
    {
        _isScrubbing = false;
        _player?.Seek(_seekValue);
    }

    public void ToggleDescription() => IsDescriptionExpanded = !IsDescriptionExpanded;

    public async Task DownloadAsync(DownloadProfile profile)
    {
        IsDownloading = true;
        DownloadMessage = null;
        DownloadSucceeded = false;
        try
        {
            var result = await _enqueueCoordinator.TryEnqueueAsync(WatchUrl, profile.Id);
            DownloadSucceeded = result.IsSuccess;
            DownloadMessage = result.IsSuccess ? "Added to downloads." : result.Error;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _noticeCts?.Cancel();
        _noticeCts?.Dispose();
        _noticeCts = null;
        _commentsCts?.Dispose();
        _commentsCts = null;
        _profilesCts?.Dispose();
        _profilesCts = null;
        if (_player is null)
            return;

        _player.PositionChanged -= OnPositionChanged;
        _player.LengthChanged -= OnLengthChanged;
        _player.IsPlayingChanged -= OnIsPlayingChanged;
        _player.Failed -= OnPlayerFailed;
        _player.Dispose();
    }

    private async Task LoadDetailsAndPlayAsync(CancellationToken token)
    {
        try
        {
            var details = await RunWithTimeoutAsync(
                DetailsTimeout,
                "Loading the video",
                token,
                t => _detailsService.GetDetailsAsync(WatchUrl, t));
            token.ThrowIfCancellationRequested();
            _details = details;
            if (details.DurationSeconds is > 0 && _lengthSeconds <= 0)
                LengthSeconds = details.DurationSeconds.Value;

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Channel));
            OnPropertyChanged(nameof(StatsLine));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(HasDescription));
            OnPropertyChanged(nameof(CanExpandDescription));

            if (_player is null)
                return;

            if (string.IsNullOrEmpty(details.VideoStreamUrl))
                PlaybackError = "No playable stream was found for this video.";
            else
                _player.Play(details.VideoStreamUrl, details.AudioStreamUrl);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DetailsError = ex.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoadingDetails = false;
        }
    }

    private async Task LoadCommentsAsync(CancellationToken pageToken)
    {
        var token = BeginOperation(ref _commentsCts, pageToken);
        IsLoadingComments = true;
        CommentsError = null;
        try
        {
            var comments = await RunWithTimeoutAsync(
                CommentsTimeout,
                "Loading comments",
                token,
                t => _commentsService.GetTopCommentsAsync(WatchUrl, t));
            token.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            Comments.Clear();
            foreach (var comment in comments)
                Comments.Add(new CommentViewModel(comment, now));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            CommentsError = ex.Message;
        }

        IsLoadingComments = false;
        RaiseCommentStateChanged();
    }

    private async Task LoadSegmentsAsync(CancellationToken token)
    {
        try
        {
            var config = await _appConfigStore.LoadAsync(token);
            if (!config.SponsorBlockEnabled)
                return;

            var segments = await _sponsorBlock.GetSkipSegmentsAsync(_result.VideoId, token);
            token.ThrowIfCancellationRequested();
            _segments = segments;
            OnPropertyChanged(nameof(Segments));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // Skipping is a convenience; playback must not depend on it.
            System.Diagnostics.Trace.TraceWarning($"SponsorBlock lookup failed: {ex.Message}");
        }
    }

    private async Task LoadProfilesAsync(CancellationToken pageToken)
    {
        var token = BeginOperation(ref _profilesCts, pageToken);
        try
        {
            var (profiles, selected) = await ProfileListLoader.LoadWithSelectionAsync(_profileStore, _appConfigStore, token);
            token.ThrowIfCancellationRequested();
            Profiles.Clear();
            if (selected is not null)
                Profiles.Add(selected);
            foreach (var profile in profiles.Where(p => p.Id != selected?.Id))
                Profiles.Add(profile);
            OnPropertyChanged(nameof(Profiles));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not load profiles for the watch page: {ex.Message}");
        }
    }

    /// <summary>Re-reads profiles after the Profiles settings changed; the active one is listed first.</summary>
    public Task RefreshProfilesAsync() => LoadProfilesAsync(_cts.Token);

    private void OnPositionChanged(double seconds)
    {
        _positionSeconds = seconds;
        if (!_isScrubbing)
            SeekValue = seconds;

        OnPropertyChanged(nameof(TimeText));

        if (_segments.Count == 0)
            return;

        var skip = SponsorSegmentSkipper.NextSeekTarget(seconds, _segments, _skipped);
        if (skip is null)
            return;

        foreach (var uuid in skip.Uuids)
            _skipped.Add(uuid);

        _player?.Seek(skip.SeekTo);
        _ = ShowSkipNoticeAsync();
    }

    private async Task ShowSkipNoticeAsync()
    {
        _noticeCts?.Cancel();
        _noticeCts?.Dispose();
        var cts = _noticeCts = new CancellationTokenSource();
        SkipNotice = "Skipped sponsor segment";
        try
        {
            await Task.Delay(SkipNoticeDuration, cts.Token);
            SkipNotice = null;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnLengthChanged(double seconds)
    {
        if (seconds > 0)
            LengthSeconds = seconds;
    }

    private void OnIsPlayingChanged(bool playing) => IsPlaying = playing;

    private void OnPlayerFailed(string message) => PlaybackError = message;

    private void RaiseCommentStateChanged()
    {
        OnPropertyChanged(nameof(HasComments));
        OnPropertyChanged(nameof(HasCommentsError));
        OnPropertyChanged(nameof(ShowNoComments));
        OnPropertyChanged(nameof(CommentsHeader));
    }

    private static string Format(double seconds) =>
        DurationFormatter.Format((int)seconds) is { Length: > 0 } text ? text : "0:00";
}
