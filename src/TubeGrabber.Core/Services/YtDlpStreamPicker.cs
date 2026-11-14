// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Services;

/// <summary>One entry of the yt-dlp <c>formats</c> array, reduced to what stream picking needs.</summary>
public sealed record StreamFormat(
    string? Url,
    string? VideoCodec,
    string? AudioCodec,
    string? Extension,
    string? Protocol,
    string? FormatNote,
    int? Height,
    double? TotalBitrate,
    double? AudioBitrate);

public sealed record PickedStreams(string? VideoUrl, string? AudioUrl);

/// <summary>Pure selection of playable stream URLs from yt-dlp formats.</summary>
public static class YtDlpStreamPicker
{
    public const int MaxHeight = 1080;

    public static PickedStreams Pick(IEnumerable<StreamFormat> formats)
    {
        var usable = formats
            .Where(f => !string.IsNullOrWhiteSpace(f.Url) && !IsStoryboard(f))
            .ToList();

        var video = PickBest(
            usable.Where(f => !IsNone(f.VideoCodec) && IsNone(f.AudioCodec) && (f.Height ?? 0) <= MaxHeight),
            f => (IsPreferredVideo(f) ? 1 : 0, f.Height ?? 0, f.TotalBitrate ?? 0));
        var audio = PickBest(
            usable.Where(f => !IsNone(f.AudioCodec) && IsNone(f.VideoCodec)),
            f => (string.Equals(f.Extension, "m4a", StringComparison.OrdinalIgnoreCase) ? 1 : 0, 0, f.AudioBitrate ?? 0));

        if (video is not null && audio is not null)
            return new PickedStreams(video.Url, audio.Url);

        var muxed = PickBest(
            usable.Where(f => !IsNone(f.VideoCodec) && !IsNone(f.AudioCodec)),
            f => (0, f.Height ?? 0, f.TotalBitrate ?? 0));
        return new PickedStreams(muxed?.Url, null);
    }

    private static StreamFormat? PickBest(
        IEnumerable<StreamFormat> candidates,
        Func<StreamFormat, (int Preferred, int Height, double Bitrate)> rank)
    {
        var list = candidates.ToList();
        // HLS manifests are only a last resort: direct HTTP streams play more reliably.
        var direct = list.Where(f => !IsHls(f)).ToList();
        return (direct.Count > 0 ? direct : list)
            .OrderByDescending(f => rank(f).Preferred)
            .ThenByDescending(f => rank(f).Height)
            .ThenByDescending(f => rank(f).Bitrate)
            .FirstOrDefault();
    }

    private static bool IsNone(string? codec) =>
        string.IsNullOrWhiteSpace(codec) || codec == "none";

    private static bool IsHls(StreamFormat f) =>
        f.Protocol?.Contains("m3u8", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsPreferredVideo(StreamFormat f) =>
        f.VideoCodec?.StartsWith("avc1", StringComparison.OrdinalIgnoreCase) == true
        || string.Equals(f.Extension, "mp4", StringComparison.OrdinalIgnoreCase);

    private static bool IsStoryboard(StreamFormat f) =>
        string.Equals(f.FormatNote, "storyboard", StringComparison.OrdinalIgnoreCase)
        || (IsNone(f.VideoCodec) && IsNone(f.AudioCodec));
}
