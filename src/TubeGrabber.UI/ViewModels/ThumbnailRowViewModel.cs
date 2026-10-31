// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Media.Imaging;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

public abstract class ThumbnailRowViewModel : ViewModelBase
{
    private readonly IThumbnailLoader _thumbnailLoader;
    private readonly string? _thumbnailUrl;
    private Bitmap? _thumbnail;
    private bool _isLoadingThumbnail;

    protected ThumbnailRowViewModel(string? thumbnailUrl, IThumbnailLoader thumbnailLoader)
    {
        _thumbnailUrl = thumbnailUrl;
        _thumbnailLoader = thumbnailLoader;
    }

    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    public bool IsLoadingThumbnail
    {
        get => _isLoadingThumbnail;
        private set => SetProperty(ref _isLoadingThumbnail, value);
    }

    public async Task LoadThumbnailAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_thumbnailUrl))
            return;

        IsLoadingThumbnail = true;
        try
        {
            Thumbnail = await _thumbnailLoader.LoadAsync(_thumbnailUrl, cancellationToken);
        }
        finally
        {
            IsLoadingThumbnail = false;
        }
    }
}
