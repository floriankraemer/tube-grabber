// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

/// <summary>A video tile in the Browse grid: thumbnail, title, channel and duration badge.</summary>
public sealed class VideoTileViewModel : ThumbnailRowViewModel
{
    public VideoTileViewModel(YouTubeSearchResult result, IThumbnailLoader thumbnailLoader)
        : base(result.ThumbnailUrl, thumbnailLoader)
    {
        Result = result;
        Title = result.Title;
        Channel = result.Channel ?? string.Empty;
        DurationDisplay = DurationFormatter.Format(result.DurationSeconds);
    }

    public YouTubeSearchResult Result { get; }
    public string Title { get; }
    public string Channel { get; }
    public string DurationDisplay { get; }
}
