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
/// The player arrives asynchronously (the playback engine starts up off the UI thread), so the page is usable
/// while it is still pending.
/// </summary>
public sealed class WatchViewModel : ViewModelBase, IDisposable
{
    // Position ticks further apart than this mean the user sought rather than played on.
    private const double MaxPlaybackStepSeconds = 2;

    private readonly YouTubeSearchResult _result;
    private readonly IVideoDetailsService _detailsService;
    private readonly IVideoCommentsService _commentsService;
    private readonly ISponsorBlockClient _sponsorBlock;
    private readonly DownloadEnqueueCoordinator _enqueueCoordinator;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly Task<IVideoPlayer?> _playerReady;
    private IVideoPlayer? _player;
    private bool _playerResolved;
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
    private bool _isBuffering;
    private double _lastPosition;
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
    private bool _playbackRetried;
    private bool _hasPlayed;
    private bool _disposed;

    public WatchViewModel(
        YouTubeSearchResult result,
        IVideoDetailsService detailsService,
        IVideoCommentsService commentsService,
        ISponsorBlockClient sponsorBlock,
        DownloadEnqueueCoordinator enqueueCoordinator,
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        Task<IVideoPlayer?> player)
    {
        _result = result;
        _detailsService = detailsService;
        _commentsService = commentsService;
        _sponsorBlock = sponsorBlock;
        _enqueueCoordinator = enqueueCoordinator;
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _lengthSeconds = result.DurationSeconds ?? 0;
        _playerReady = AttachPlayerAsync(player);
    }

    /// <summary>yt-dlp can hang on a bad network or extractor; the page then shows an error with Retry.</summary>
    public TimeSpan DetailsTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Comment extraction pages through the API and is slower than the details lookup.</summary>
    public TimeSpan CommentsTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>How long the "Skipped sponsor segment" notice stays visible.</summary>
    public TimeSpan SkipNoticeDuration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Upper bound for waiting until fresh stream URLs become valid; guards against a skewed clock.</summary>
    public TimeSpan MaxStreamWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Null until the player arrived, and for good when playback is unavailable.</summary>
    public IVideoPlayer? Player => _player;

    /// <summary>False only once it is known that there is no player; true while it is still starting up.</summary>
    public bool IsPlaybackAvailable => !_playerResolved || _player is not null;
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

    /// <summary>The player is waiting for data, for example after a seek; shown as a spinner in the control bar.</summary>
    public bool IsBuffering
    {
        get => _isBuffering;
        private set => SetProperty(ref _isBuffering, value);
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

    /// <summary>The notice line is only reserved when segments exist, so it never shifts the page below the player.</summary>
    public bool HasSegments => _segments.Count > 0;

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
        _lastPosition = 0;
        _playbackRetried = false;

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

    /// <summary>Call after the video surface changed size: a paused picture is redrawn by stepping one frame.</summary>
    public void RedrawPausedFrame()
    {
        if (_player is not null && _hasPlayed && !IsPlaying)
            _player.StepFrame();
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
        _player.BufferingChanged -= OnBufferingChanged;
        _player.Failed -= OnPlayerFailed;
        _player.Dispose();
    }

    // Completes once the player (or its absence) is known and announced, so the view has bound the video surface
    // before anything is played.
    private async Task<IVideoPlayer?> AttachPlayerAsync(Task<IVideoPlayer?> pending)
    {
        IVideoPlayer? player = null;
        try
        {
            player = await pending;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Could not create the video player: {ex.Message}");
        }

        if (_disposed)
        {
            player?.Dispose();
            return null;
        }

        _playerResolved = true;
        if (player is not null)
        {
            player.PositionChanged += OnPositionChanged;
            player.LengthChanged += OnLengthChanged;
            player.IsPlayingChanged += OnIsPlayingChanged;
            player.BufferingChanged += OnBufferingChanged;
            player.Failed += OnPlayerFailed;
            // The volume slider may have been moved while the player was still starting up.
            if (player.Volume != _volume)
                player.Volume = _volume;
            _player = player;
        }

        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(IsPlaybackAvailable));
        OnPropertyChanged(nameof(ShowPlaybackSpinner));
        return player;
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

            var player = await _playerReady;
            token.ThrowIfCancellationRequested();
            if (player is null)
                return;

            if (string.IsNullOrEmpty(details.VideoStreamUrl))
                PlaybackError = "No playable stream was found for this video.";
            else
            {
                await WaitUntilStreamsAvailableAsync(details.StreamsAvailableAt, token);
                player.Play(details.VideoStreamUrl, details.AudioStreamUrl);
            }
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

    // googlevideo rejects a URL with 403 until yt-dlp's available_at; playing earlier fails intermittently.
    private async Task WaitUntilStreamsAvailableAsync(DateTimeOffset? availableAt, CancellationToken token)
    {
        if (availableAt is null)
            return;

        var deadline = availableAt.Value;
        var cap = DateTimeOffset.UtcNow + MaxStreamWait;
        if (deadline > cap)
            deadline = cap;

        // Task.Delay can return a few milliseconds early on coarse timers, so re-check the wall clock.
        for (var remaining = deadline - DateTimeOffset.UtcNow; remaining > TimeSpan.Zero;
             remaining = deadline - DateTimeOffset.UtcNow)
        {
            await Task.Delay(remaining, token);
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
            OnPropertyChanged(nameof(HasSegments));
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
        var previous = _lastPosition;
        _lastPosition = seconds;
        _positionSeconds = seconds;
        if (!_isScrubbing)
            SeekValue = seconds;

        OnPropertyChanged(nameof(TimeText));

        if (_segments.Count == 0)
            return;

        // A segment skipped earlier is armed again once the position is back before its start (seek back, replay).
        foreach (var segment in _segments)
        {
            if (seconds < segment.StartSeconds)
                _skipped.Remove(segment.Uuid);
        }

        var skip = SponsorSegmentSkipper.NextSeekTarget(seconds, _segments, _skipped);
        if (skip is null)
            return;

        foreach (var uuid in skip.Uuids)
            _skipped.Add(uuid);

        // Like the browser extension: skip only when playback runs into a segment from before its start. Landing
        // inside one by a deliberate seek leaves it alone.
        if (!EnteredByPlayback(previous, seconds, skip))
            return;

        _player?.Seek(skip.SeekTo);
        _ = ShowSkipNoticeAsync();
    }

    // Position ticks arrive a few times per second; a larger jump, or one from after the start, was a seek.
    private bool EnteredByPlayback(double previous, double position, SponsorSkip skip)
    {
        var start = _segments.Where(s => skip.Uuids.Contains(s.Uuid)).Min(s => s.StartSeconds);
        return previous <= start && position - previous <= MaxPlaybackStepSeconds;
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

    private void OnIsPlayingChanged(bool playing)
    {
        _hasPlayed |= playing;
        IsPlaying = playing;
    }

    private void OnBufferingChanged(bool buffering) => IsBuffering = buffering;

    // Stream URLs can fail for reasons a fresh lookup fixes (expired, rejected before they became valid), so the
    // first failure of a load silently resolves new ones and resumes; only a second failure is shown.
    private void OnPlayerFailed(string message)
    {
        if (_disposed || _player is null || _playbackRetried)
        {
            PlaybackError = message;
            return;
        }

        _playbackRetried = true;
        _ = RetryPlaybackAsync(message, _cts.Token);
    }

    private async Task RetryPlaybackAsync(string message, CancellationToken token)
    {
        IsLoadingDetails = true;
        try
        {
            var details = await RunWithTimeoutAsync(
                DetailsTimeout,
                "Loading the video",
                token,
                t => _detailsService.GetDetailsAsync(WatchUrl, t));
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(details.VideoStreamUrl))
            {
                PlaybackError = message;
                return;
            }

            await WaitUntilStreamsAvailableAsync(details.StreamsAvailableAt, token);
            _player!.Play(details.VideoStreamUrl, details.AudioStreamUrl, _lastPosition);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            PlaybackError = message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoadingDetails = false;
        }
    }

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
