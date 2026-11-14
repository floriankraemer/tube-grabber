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
    private Media? _media;
    private bool _disposed;

    public VlcVideoPlayer(LibVLC libVlc)
    {
        _libVlc = libVlc;
        MediaPlayer = new MediaPlayer(libVlc);
        MediaPlayer.TimeChanged += OnTimeChanged;
        MediaPlayer.LengthChanged += OnLengthChanged;
        MediaPlayer.Playing += OnPlaying;
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
    public event Action<string>? Failed;

    public int Volume
    {
        get => Math.Max(0, MediaPlayer.Volume);
        set => MediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public void Play(string videoUrl, string? audioUrl)
    {
        var media = new Media(_libVlc, new Uri(videoUrl));
        if (!string.IsNullOrEmpty(audioUrl))
            media.AddOption(":input-slave=" + audioUrl);

        var previous = _media;
        _media = media;
        MediaPlayer.Play(media);
        previous?.Dispose();
    }

    public void Pause() => MediaPlayer.SetPause(true);

    public void Resume() => MediaPlayer.SetPause(false);

    // Off the caller's thread: libvlc calls can block, and Seek is invoked from handlers of VLC-derived events.
    public void Seek(double seconds) => Task.Run(() => MediaPlayer.Time = (long)(seconds * 1000));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        MediaPlayer.TimeChanged -= OnTimeChanged;
        MediaPlayer.LengthChanged -= OnLengthChanged;
        MediaPlayer.Playing -= OnPlaying;
        MediaPlayer.Paused -= OnPaused;
        MediaPlayer.Stopped -= OnPaused;
        MediaPlayer.EndReached -= OnPaused;
        MediaPlayer.EncounteredError -= OnError;
        var player = MediaPlayer;
        var media = _media;
        // Stop() can block; the MediaPlayer must go before LibVLC itself, which PlaybackEngine disposes at exit.
        Task.Run(() =>
        {
            player.Stop();
            player.Dispose();
            media?.Dispose();
        });
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e) =>
        Post(() => PositionChanged?.Invoke(e.Time / 1000.0));

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e) =>
        Post(() => LengthChanged?.Invoke(e.Length / 1000.0));

    private void OnPlaying(object? sender, EventArgs e) => Post(() => IsPlayingChanged?.Invoke(true));

    private void OnPaused(object? sender, EventArgs e) => Post(() => IsPlayingChanged?.Invoke(false));

    private void OnError(object? sender, EventArgs e) =>
        Post(() => Failed?.Invoke("This video could not be played. Try again, or open it in your browser."));

    private void Post(Action action)
    {
        if (!_disposed)
            Dispatcher.UIThread.Post(action);
    }
}
