// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Threading;
using LibVLCSharp.Shared;

namespace TubeGrabber.UI.Services;

/// <summary>LibVLC-backed player. Native events arrive on VLC threads; they are re-posted to the UI thread.</summary>
public sealed class VlcVideoPlayer : IVideoPlayer
{
    private readonly LibVLC _libVlc;
    private readonly SerialCommandQueue _commands = new();
    private readonly TaskCompletionSource _teardown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Media? _media;
    private int _volume = 100;
    private int _disposedFlag;

    public VlcVideoPlayer(LibVLC libVlc)
    {
        _libVlc = libVlc;
        MediaPlayer = new MediaPlayer(libVlc);
        _volume = Math.Max(0, MediaPlayer.Volume);
        MediaPlayer.TimeChanged += OnTimeChanged;
        MediaPlayer.LengthChanged += OnLengthChanged;
        MediaPlayer.Playing += OnPlaying;
        MediaPlayer.Buffering += OnBuffering;
        MediaPlayer.Paused += OnPaused;
        MediaPlayer.Stopped += OnPaused;
        MediaPlayer.EndReached += OnPaused;
        MediaPlayer.EncounteredError += OnError;
    }

    /// <summary>Attached to the VideoView so the video renders inside the window.</summary>
    public MediaPlayer MediaPlayer { get; }

    public event Action<double>? PositionChanged;
    public event Action<double>? LengthChanged;
    public event Action<bool>? IsPlayingChanged;
    public event Action<bool>? BufferingChanged;
    public event Action<string>? Failed;

    /// <summary>Completes once the native player is stopped and released; never completes before <see cref="Dispose"/>.</summary>
    public Task TeardownTask => _teardown.Task;

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            Enqueue(() => MediaPlayer.Volume = _volume, "volume");
        }
    }

    public void Play(string videoUrl, string? audioUrl, double startSeconds = 0) =>
        Enqueue(() =>
        {
            var media = new Media(_libVlc, new Uri(videoUrl));
            // VLC's own MP4 demuxer snaps a seek to the previous DASH fragment (2-5 s early) once audio runs as an
            // input slave; the libavformat demuxer seeks to the requested time.
            media.AddOption(":demux=avformat");
            if (!string.IsNullOrEmpty(audioUrl))
                media.AddOption(":input-slave=" + audioUrl);
            if (startSeconds > 0)
                media.AddOption(FormattableString.Invariant($":start-time={startSeconds:0.###}"));

            var previous = _media;
            _media = media;
            MediaPlayer.Play(media);
            previous?.Dispose();
        }, "play");

    public void Pause() => Enqueue(() => MediaPlayer.SetPause(true), "pause");

    public void Resume() => Enqueue(() => MediaPlayer.SetPause(false), "pause");

    // Queued, never run inline: libvlc calls can block, and Seek is invoked from handlers of VLC-derived events.
    // Rapid seeks coalesce so the latest target wins.
    public void Seek(double seconds) => Enqueue(() => MediaPlayer.Time = (long)(seconds * 1000), "seek");

    public void StepFrame() => Enqueue(MediaPlayer.NextFrame, "step");

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposedFlag, 1) == 1)
            return;

        MediaPlayer.TimeChanged -= OnTimeChanged;
        MediaPlayer.LengthChanged -= OnLengthChanged;
        MediaPlayer.Playing -= OnPlaying;
        MediaPlayer.Buffering -= OnBuffering;
        MediaPlayer.Paused -= OnPaused;
        MediaPlayer.Stopped -= OnPaused;
        MediaPlayer.EndReached -= OnPaused;
        MediaPlayer.EncounteredError -= OnError;
        // Pending commands are dropped; the teardown runs after the one in flight. The MediaPlayer must go before
        // LibVLC itself, which PlaybackEngine disposes only after TeardownTask completes.
        _ = _commands.Close(() =>
        {
            try
            {
                MediaPlayer.Stop();
                MediaPlayer.Dispose();
                _media?.Dispose();
            }
            finally
            {
                _teardown.TrySetResult();
            }
        });
    }

    private void Enqueue(Action command, string key)
    {
        if (!IsDisposed)
            _commands.Enqueue(command, key);
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e) =>
        Post(() => PositionChanged?.Invoke(e.Time / 1000.0));

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e) =>
        Post(() => LengthChanged?.Invoke(e.Length / 1000.0));

    private void OnPlaying(object? sender, EventArgs e) => Post(() => IsPlayingChanged?.Invoke(true));

    // VLC reports the cache fill level; below 100 the picture is stalled.
    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e) =>
        Post(() => BufferingChanged?.Invoke(e.Cache < 100f));

    private void OnPaused(object? sender, EventArgs e) => Post(() => IsPlayingChanged?.Invoke(false));

    private void OnError(object? sender, EventArgs e) =>
        Post(() => Failed?.Invoke("This video could not be played. Try again, or open it in your browser."));

    private bool IsDisposed => Volatile.Read(ref _disposedFlag) == 1;

    private void Post(Action action)
    {
        if (!IsDisposed)
            Dispatcher.UIThread.Post(action);
    }
}
