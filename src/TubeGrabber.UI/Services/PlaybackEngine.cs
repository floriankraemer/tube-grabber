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
    private bool _initialized;

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

            return _libVlc is null ? null : new VlcVideoPlayer(_libVlc);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _libVlc?.Dispose();
            _libVlc = null;
        }
    }
}
