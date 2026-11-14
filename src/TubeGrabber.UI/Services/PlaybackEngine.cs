// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using LibVLCSharp.Shared;

namespace TubeGrabber.UI.Services;

/// <summary>
/// Owns the one shared LibVLC instance. Initialization is lazy and failure-tolerant: without libvlc the app
/// still works, the watch page just offers "Open in browser".
/// </summary>
public sealed class PlaybackEngine : IDisposable
{
    private readonly object _gate = new();
    private LibVLC? _libVlc;
    private readonly List<VlcVideoPlayer> _players = new();
    private bool _initialized;

    /// <summary>How long Dispose waits for player teardowns before giving up on releasing LibVLC.</summary>
    public static readonly TimeSpan TeardownTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Creates a player, or null when libvlc cannot be loaded.</summary>
    public VlcVideoPlayer? CreatePlayer()
    {
        lock (_gate)
        {
            if (!_initialized)
            {
                _initialized = true;
                try
                {
                    LibVLCSharp.Shared.Core.Initialize();
                    _libVlc = new LibVLC("--no-xlib");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError($"libvlc is not available: {ex.Message}");
                }
            }

            if (_libVlc is null)
                return null;

            var player = new VlcVideoPlayer(_libVlc);
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
            live = _players.ToArray();

        // Players still open at quit are stopped here, then every teardown is awaited before LibVLC goes.
        foreach (var player in live)
            player.Dispose();

        var tornDown = Task.WhenAll(live.Select(p => p.TeardownTask)).Wait(TeardownTimeout);
        lock (_gate)
        {
            if (!tornDown)
            {
                // Disposing LibVLC under a live MediaPlayer can crash; the process is exiting, so leak instead.
                System.Diagnostics.Trace.TraceWarning("A video player did not stop in time; leaving libvlc to process exit.");
                return;
            }

            _libVlc?.Dispose();
            _libVlc = null;
        }
    }
}
