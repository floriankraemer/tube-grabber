// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

/// <summary>
/// Playback seam for the watch view. Implementations raise every event on the UI thread, after the native
/// callback has returned, so handlers may call back into the player (for example to seek).
/// </summary>
public interface IVideoPlayer : IDisposable
{
    /// <summary>Playback position in seconds.</summary>
    event Action<double>? PositionChanged;

    /// <summary>Media length in seconds, once known.</summary>
    event Action<double>? LengthChanged;

    event Action<bool>? IsPlayingChanged;

    /// <summary>True while the player waits for data (initial load or after a seek); false once playback can continue.</summary>
    event Action<bool>? BufferingChanged;

    /// <summary>Playback failed; the argument is a user-facing message.</summary>
    event Action<string>? Failed;

    int Volume { get; set; }

    void Play(string videoUrl, string? audioUrl);

    void Pause();

    void Resume();

    void Seek(double seconds);
}
