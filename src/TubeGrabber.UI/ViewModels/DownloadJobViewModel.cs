// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.UI.ViewModels;

public sealed class DownloadJobViewModel : ViewModelBase
{
    private readonly DownloadJob _job;

    public DownloadJobViewModel(DownloadJob job) => _job = job;

    public Guid Id => _job.Id;
    public string Url => _job.Url;
    public string? Title => YtDlpMetadataParser.FormatQueueTitle(_job);
    public DownloadStatus Status => _job.Status;
    public string StatusText => Status switch
    {
        DownloadStatus.Running when _job.ProgressPhase == DownloadProgressPhase.PostProcessing => "Processing",
        _ => Status.ToString(),
    };
    public double Progress => _job.Progress;
    public bool IsProgressIndeterminate =>
        _job.Status == DownloadStatus.Running && _job.UseIndeterminateProgress;
    public string ProgressDisplayText => IsProgressIndeterminate
        ? _job.ProgressActivity ?? "Processing…"
        : $"{Progress:0}%";
    public string? Speed => _job.ProgressPhase == DownloadProgressPhase.PostProcessing
        ? _job.ProgressActivity ?? _job.Speed
        : _job.Speed;
    public string? Eta => _job.Eta;
    public string? Error => _job.Error;
    public string? LogOutput => _job.LogOutput;
    public bool CanViewLog => _job.Status == DownloadStatus.Failed;
    public bool CanStart => _job.Status is DownloadStatus.Queued or DownloadStatus.Failed;
    public bool CanCancel => _job.Status is DownloadStatus.Queued or DownloadStatus.Running;
    public bool CanRemove => _job.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;
    public bool CanOpenOutput => DownloadOutputResolver.CanOpen(_job);
    public string OpenOutputMenuLabel => DownloadOutputResolver.IsSingleFile(_job) ? "Open file" : "Open Location";

    public bool CanRetry => _job.Status == DownloadStatus.Failed;

    /// <summary>Queued jobs get a play button; failed ones get a retry button instead.</summary>
    public bool CanPlay => _job.Status == DownloadStatus.Queued;

    private bool IsPostProcessing =>
        _job.Status == DownloadStatus.Running && _job.ProgressPhase == DownloadProgressPhase.PostProcessing;

    /// <summary>Drives the status icon and colour in the queue: active, processing, queued, failed, completed or cancelled.</summary>
    public string StatusKind => Status switch
    {
        DownloadStatus.Running when IsPostProcessing => "processing",
        DownloadStatus.Running => "active",
        DownloadStatus.Failed => "failed",
        DownloadStatus.Completed => "completed",
        DownloadStatus.Cancelled => "cancelled",
        _ => "queued",
    };

    /// <summary>Second line under the title: the failure reason, the phase, the saved file, or the source URL.</summary>
    public string Detail => Status switch
    {
        DownloadStatus.Failed when !string.IsNullOrWhiteSpace(_job.Error) => _job.Error!,
        DownloadStatus.Running when IsPostProcessing => _job.ProgressActivity ?? "Post-processing",
        DownloadStatus.Completed when _job.OutputPaths.Count > 0 => Path.GetFileName(_job.OutputPaths[0]),
        _ => Url,
    };

    public bool IsDetailError => Status == DownloadStatus.Failed && !string.IsNullOrWhiteSpace(_job.Error);

    /// <summary>Caption under the progress bar, built only from the fields the job actually carries.</summary>
    /// <remarks>Speed is left out to keep the caption short enough for the column; see <see cref="ProgressToolTip"/>.</remarks>
    public string ProgressDetail => BuildProgressDetail(includeSpeed: false);

    /// <summary>Full progress line including the download speed.</summary>
    public string ProgressToolTip => BuildProgressDetail(includeSpeed: true);

    private string BuildProgressDetail(bool includeSpeed)
    {
        switch (Status)
        {
            case DownloadStatus.Queued:
                return "Waiting for a free slot";
            case DownloadStatus.Completed:
                return "Completed";
            case DownloadStatus.Failed or DownloadStatus.Cancelled:
                return Progress > 0 ? $"Stopped at {Progress:0}%" : "Not started";
        }

        if (IsPostProcessing || IsProgressIndeterminate)
            return "Post-processing";

        var parts = new List<string>
        {
            string.IsNullOrWhiteSpace(_job.TotalSize) ? $"{Progress:0}%" : $"{Progress:0}% of {_job.TotalSize}",
        };
        if (includeSpeed && !string.IsNullOrWhiteSpace(_job.Speed))
            parts.Add(_job.Speed!);
        if (!string.IsNullOrWhiteSpace(_job.Eta))
            parts.Add($"ETA {_job.Eta}");
        return string.Join(" · ", parts);
    }

    public void Refresh() => OnPropertyChanged(string.Empty);
}
