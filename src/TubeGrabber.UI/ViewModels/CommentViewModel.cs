// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.ViewModels;

public sealed class CommentViewModel
{
    public CommentViewModel(VideoComment comment, DateTimeOffset now)
    {
        Author = comment.Author;
        Text = comment.Text;
        IsPinned = comment.IsPinned;
        IsUploader = comment.AuthorIsUploader;
        Likes = comment.LikeCount is > 0 ? comment.LikeCount.Value.ToString("N0") : string.Empty;
        PostedAgo = comment.PostedAt is { } posted ? RelativeTime(now - posted) : string.Empty;
    }

    public string Author { get; }
    public string Text { get; }
    public bool IsPinned { get; }
    public bool IsUploader { get; }
    public string Likes { get; }
    public string PostedAgo { get; }

    internal static string RelativeTime(TimeSpan age)
    {
        if (age.TotalMinutes < 1)
            return "just now";
        if (age.TotalHours < 1)
            return Unit((int)age.TotalMinutes, "minute");
        if (age.TotalDays < 1)
            return Unit((int)age.TotalHours, "hour");
        if (age.TotalDays < 30)
            return Unit((int)age.TotalDays, "day");
        if (age.TotalDays < 365)
            return Unit((int)(age.TotalDays / 30), "month");
        return Unit((int)(age.TotalDays / 365), "year");
    }

    private static string Unit(int count, string unit) => $"{count} {unit}{(count == 1 ? "" : "s")} ago";
}
