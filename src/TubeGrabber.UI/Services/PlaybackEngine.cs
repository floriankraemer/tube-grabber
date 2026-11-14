// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using LibVLCSharp.Shared;

namespace TubeGrabber.UI.Services;

/// <summary>
/// Owns the one shared LibVLC instance. Initialization is lazy, failure-tolerant and runs off the UI thread:
/// libvlc scans its whole plugins folder on creation, which takes from a fraction of a second on a local disk to
/// more than half a minute on a slow or network drive. Without libvlc the app still works, the watch page just
/// offers "Open in browser".
/// </summary>
public sealed class PlaybackEngine : IDisposable
{
    private readonly object _gate = new();
    private readonly Lazy<Task<LibVLC?>> _libVlc = new(() => Task.Run(Initialize));
    private readonly List<VlcVideoPlayer> _players = new();
    private bool _disposed;

    /// <summary>How long Dispose waits for player teardowns before giving up on releasing LibVLC.</summary>
    public static readonly TimeSpan TeardownTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Creates a player, or null when libvlc cannot be loaded or the engine is already disposed.</summary>
    public async Task<IVideoPlayer?> CreatePlayerAsync()
    {
        var libVlc = await _libVlc.Value;
        lock (_gate)
        {
            if (libVlc is null || _disposed)
                return null;

            var player = new VlcVideoPlayer(libVlc);
            _players.Add(player);
            // Teardown runs off-thread after the player is disposed; LibVLC must outlive it.
            _ = player.TeardownTask.ContinueWith(_ => { lock (_gate) _players.Remove(player); }, TaskScheduler.Default);
            return player;
        }
    }

    public void Dispose()
    {
        VlcVideoPlayer[] live;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            live = _players.ToArray();
        }

        // Players still open at quit are stopped here, then every teardown is awaited before LibVLC goes.
        foreach (var player in live)
            player.Dispose();

        if (!Task.WhenAll(live.Select(p => p.TeardownTask)).Wait(TeardownTimeout))
        {
            // Disposing LibVLC under a live MediaPlayer can crash; the process is exiting, so leak instead.
            System.Diagnostics.Trace.TraceWarning("A video player did not stop in time; leaving libvlc to process exit.");
            return;
        }

        // An initialization still in flight is left to process exit as well.
        if (_libVlc.IsValueCreated && _libVlc.Value.IsCompletedSuccessfully)
            _libVlc.Value.Result?.Dispose();
    }

    private static LibVLC? Initialize()
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            // Input stays with Avalonia: the native video window must not take keys (Esc leaves fullscreen) or clicks.
            return new LibVLC("--no-xlib", "--no-keyboard-events", "--no-mouse-events");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"libvlc is not available: {ex.Message}");
            return null;
        }
    }
}
