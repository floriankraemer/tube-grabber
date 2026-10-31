// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.Concurrent;
using System.Threading.Channels;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class DownloadQueueService : IDownloadQueueService
{
    private const string CancelHint = "Download cancelled. A partial file may remain on disk.";

    private readonly IYtDlpProcessRunner _processRunner;
    private readonly YtDlpCommandBuilder _commandBuilder;
    private readonly IAppConfigStore _appConfigStore;
    private readonly IProfileStore _profileStore;
    private readonly IBinaryLocator _binaryLocator;
    private readonly YouTubeUrlNormalizer _urlNormalizer;
    private readonly YtDlpProgressParser _progressParser;
    private readonly YtDlpOutputPathParser _outputPathParser;
    private readonly YtDlpMetadataParser _metadataParser;
    private readonly DownloadFolderService _downloadFolderService;
    private readonly IJsRuntimeLocator _jsRuntimeLocator;
    private readonly ConcurrentDictionary<Guid, DownloadJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCancellation = new();
    private readonly ConcurrentDictionary<Guid, byte> _activeWorkers = new();
    private readonly Channel<Guid> _pendingJobs = Channel.CreateUnbounded<Guid>();
    private readonly object _sync = new();
    private SemaphoreSlim _concurrencyGate;
    private int _maxConcurrent = AppPaths.DefaultMaxConcurrentDownloads;

    public DownloadQueueService(
        IYtDlpProcessRunner processRunner,
        YtDlpCommandBuilder commandBuilder,
        IAppConfigStore appConfigStore,
        IProfileStore profileStore,
        IBinaryLocator binaryLocator,
        YouTubeUrlNormalizer urlNormalizer,
        YtDlpProgressParser progressParser,
        YtDlpOutputPathParser outputPathParser,
        YtDlpMetadataParser metadataParser,
        DownloadFolderService downloadFolderService,
        IJsRuntimeLocator jsRuntimeLocator)
    {
        _processRunner = processRunner;
        _commandBuilder = commandBuilder;
        _appConfigStore = appConfigStore;
        _profileStore = profileStore;
        _binaryLocator = binaryLocator;
        _urlNormalizer = urlNormalizer;
        _progressParser = progressParser;
        _outputPathParser = outputPathParser;
        _metadataParser = metadataParser;
        _downloadFolderService = downloadFolderService;
        _jsRuntimeLocator = jsRuntimeLocator;
        _concurrencyGate = CreateConcurrencyGate(_maxConcurrent);
        _ = Task.Run(PumpJobsAsync);
    }

    public event EventHandler? JobsChanged;

    public IReadOnlyList<DownloadJob> Jobs
    {
        get
        {
            lock (_sync)
                return _jobs.Values.OrderBy(j => j.CreatedAt).ToList();
        }
    }

    public async Task<DownloadJob> EnqueueAsync(string url, string profileId, CancellationToken cancellationToken = default)
    {
        var normalized = _urlNormalizer.Normalize(url);
        if (!normalized.IsSuccess || string.IsNullOrWhiteSpace(normalized.NormalizedUrl))
            throw new InvalidOperationException(normalized.Error ?? "Invalid URL.");

        var config = await _appConfigStore.LoadAsync(cancellationToken);
        UpdateConcurrency(config.MaxConcurrentDownloads);

        var job = new DownloadJob
        {
            Url = normalized.NormalizedUrl,
            ProfileId = profileId,
        };

        _jobs[job.Id] = job;
        _jobCancellation[job.Id] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        NotifyJobsChanged();
        await ScheduleJobAsync(job.Id, cancellationToken);
        return job;
    }

    public Task StartJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return Task.CompletedTask;

        if (job.Status == DownloadStatus.Failed)
        {
            job.Status = DownloadStatus.Queued;
            job.Error = null;
            job.LogOutput = null;
            job.Progress = 0;
            job.ProgressPhase = DownloadProgressPhase.Downloading;
            job.ProgressActivity = null;
            job.UseIndeterminateProgress = false;
            job.Speed = null;
            job.Eta = null;
            job.TotalSize = null;
            job.WorkingDirectory = null;
            job.OutputPaths.Clear();
            job.CompletedAt = null;
            job.StartedAt = null;
            _jobCancellation[job.Id] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            NotifyJobsChanged();
        }

        if (job.Status != DownloadStatus.Queued)
            return Task.CompletedTask;

        return ScheduleJobAsync(jobId, cancellationToken);
    }

    public Task CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status is DownloadStatus.Queued or DownloadStatus.Running)
        {
            if (_jobCancellation.TryRemove(jobId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            job.Error = CancelHint;
            job.Status = DownloadStatus.Cancelled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            NotifyJobsChanged();
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobCancellation.TryRemove(jobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _jobs.TryRemove(jobId, out _);
        _activeWorkers.TryRemove(jobId, out _);
        NotifyJobsChanged();
        return Task.CompletedTask;
    }

    private Task ScheduleJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        _pendingJobs.Writer.WriteAsync(jobId, cancellationToken).AsTask();

    private async Task PumpJobsAsync()
    {
        await foreach (var jobId in _pendingJobs.Reader.ReadAllAsync())
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != DownloadStatus.Queued)
                continue;

            _ = ProcessJobAsync(job);
        }
    }

    private void UpdateConcurrency(int maxConcurrent)
    {
        var clamped = Math.Clamp(maxConcurrent, 1, AppPaths.MaxConcurrentDownloadsCap);
        lock (_sync)
        {
            if (_maxConcurrent == clamped)
                return;

            var activeSlots = _maxConcurrent - _concurrencyGate.CurrentCount;
            _maxConcurrent = clamped;
            var available = Math.Max(0, clamped - activeSlots);
            var oldGate = _concurrencyGate;
            _concurrencyGate = new SemaphoreSlim(available, clamped);
            oldGate.Dispose();
        }
    }

    private static SemaphoreSlim CreateConcurrencyGate(int maxConcurrent) =>
        new(maxConcurrent, maxConcurrent);

    private async Task ProcessJobAsync(DownloadJob job)
    {
        if (!_activeWorkers.TryAdd(job.Id, 0))
            return;

        try
        {
            if (!_jobCancellation.TryGetValue(job.Id, out var jobCts))
            {
                job.Error = "Could not start the download worker. Remove the item and add it again.";
                job.Status = DownloadStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                NotifyJobsChanged();
                return;
            }

            var acquired = false;
            YtDlpInvocation? invocation = null;
            try
            {
                if (job.Status == DownloadStatus.Cancelled)
                    return;

                await _concurrencyGate.WaitAsync(jobCts.Token);
                acquired = true;

                if (job.Status == DownloadStatus.Cancelled)
                    return;

                job.Status = DownloadStatus.Running;
                job.StartedAt = DateTimeOffset.UtcNow;
                job.Error = null;
                job.LogOutput = null;
                job.WorkingDirectory = null;
                job.OutputPaths.Clear();
                job.ProgressPhase = DownloadProgressPhase.Downloading;
                job.ProgressActivity = null;
                job.UseIndeterminateProgress = false;
                NotifyJobsChanged();

                var config = await _appConfigStore.LoadAsync(jobCts.Token);
                var profile = await _profileStore.GetAsync(job.ProfileId, jobCts.Token)
                    ?? throw new InvalidOperationException($"Profile '{job.ProfileId}' not found.");

                profile = await EnsureProfileReadyAsync(profile, jobCts.Token);

                var ytDlpPath = _binaryLocator.ResolveYtDlpPath(config)
                    ?? throw new InvalidOperationException("yt-dlp was not found. Install it or set a path in settings.");

                var ffmpegPath = _binaryLocator.ResolveFfmpegPath(config);
                if (ProfileFfmpegRequirement.RequiresFfmpeg(profile) && ffmpegPath is null)
                    throw new InvalidOperationException(ProfileFfmpegRequirement.FfmpegRequiredMessage);

                var jsRuntimesArgument = JsRuntimeArgumentBuilder.Build(config, _jsRuntimeLocator);
                var args = _commandBuilder.Build(profile, ffmpegPath, jsRuntimesArgument, job.Url);

                var workingDirectory = _downloadFolderService.ResolveWorkingDirectory(config, profile);
                invocation = new YtDlpInvocation
                {
                    ExecutablePath = ytDlpPath,
                    Arguments = args,
                    WorkingDirectory = workingDirectory,
                };
                job.WorkingDirectory = workingDirectory;

                var outputHandler = new DownloadJobOutputLineHandler(
                    job,
                    _outputPathParser,
                    _metadataParser,
                    ApplyProgressUpdate,
                    NotifyJobsChanged);

                var result = await _processRunner.RunAsync(invocation, outputHandler, jobCts.Token);
                ExtractMetadataFromLines(job, result.StandardOutput);
                ExtractMetadataFromLines(job, result.StandardError);
                job.LogOutput = YtDlpLogFormatter.Format(invocation, result);

                if (job.Status == DownloadStatus.Cancelled || result.WasCancelled)
                {
                    job.Error ??= CancelHint;
                    job.Status = DownloadStatus.Cancelled;
                }
                else if (result.ExitCode == 0)
                {
                    job.Status = DownloadStatus.Completed;
                    job.Progress = 100;
                    job.ProgressPhase = DownloadProgressPhase.Downloading;
                    job.ProgressActivity = null;
                    job.UseIndeterminateProgress = false;
                }
                else
                {
                    job.Error = YtDlpFailureMessageBuilder.Build(result);
                    job.Status = DownloadStatus.Failed;
                }

                job.CompletedAt = DateTimeOffset.UtcNow;
                NotifyJobsChanged();
            }
            catch (OperationCanceledException)
            {
                if (job.Status != DownloadStatus.Cancelled)
                {
                    job.Error ??= CancelHint;
                    job.Status = DownloadStatus.Cancelled;
                }

                job.CompletedAt = DateTimeOffset.UtcNow;
                NotifyJobsChanged();
            }
            catch (Exception ex)
            {
                if (job.Status != DownloadStatus.Cancelled)
                {
                    job.Error = ex.Message;
                    job.Status = DownloadStatus.Failed;
                    job.LogOutput = invocation is null
                        ? ex.ToString()
                        : YtDlpLogFormatter.Format(invocation, ex);
                }

                job.CompletedAt = DateTimeOffset.UtcNow;
                NotifyJobsChanged();
            }
            finally
            {
                if (_jobCancellation.TryRemove(job.Id, out var cts))
                    cts.Dispose();

                if (acquired)
                    _concurrencyGate.Release();
            }
        }
        finally
        {
            _activeWorkers.TryRemove(job.Id, out _);
        }
    }

    private void ApplyProgressUpdate(DownloadJob job, string line)
    {
        var downloadComplete = job.Progress >= 100
            || job.ProgressPhase == DownloadProgressPhase.PostProcessing;

        if (!_progressParser.TryParse(line, downloadComplete, out var update))
            return;

        job.ProgressPhase = update.Phase;
        if (update.ActivityLabel is not null)
            job.ProgressActivity = update.ActivityLabel;

        job.UseIndeterminateProgress = update.UseIndeterminateProgress;

        if (update.ProgressPercent is { } percent)
            job.Progress = percent;
        else if (update.Phase == DownloadProgressPhase.PostProcessing && job.Progress < 100)
            job.Progress = 100;

        if (update.Speed is not null)
            job.Speed = update.Speed;
        else if (update.Phase == DownloadProgressPhase.PostProcessing)
            job.Speed = null;

        if (update.TotalSize is not null)
            job.TotalSize = update.TotalSize;

        if (update.Eta is not null)
            job.Eta = update.Eta;
        else if (update.Phase == DownloadProgressPhase.PostProcessing)
            job.Eta = null;

        NotifyJobsChanged();
    }

    private async Task<DownloadProfile> EnsureProfileReadyAsync(DownloadProfile profile, CancellationToken cancellationToken)
    {
        var template = BuiltInProfiles.FindTemplate(profile.Id);
        if (template is null)
            return profile;

        if (!BuiltInProfileSynchronizer.MergeMissingFromTemplate(profile, template))
            return profile;

        await _profileStore.SaveAsync(profile, cancellationToken);
        return profile;
    }

    private void ExtractMetadataFromLines(DownloadJob job, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        foreach (var line in output.Split('\n'))
            _metadataParser.TryApplyMetadata(job, line);
    }

    private void NotifyJobsChanged() => JobsChanged?.Invoke(this, EventArgs.Empty);

    private sealed class DownloadJobOutputLineHandler : IProgress<string>
    {
        private readonly DownloadJob _job;
        private readonly YtDlpOutputPathParser _outputPathParser;
        private readonly YtDlpMetadataParser _metadataParser;
        private readonly Action<DownloadJob, string> _applyProgress;
        private readonly Action _notifyChanged;

        public DownloadJobOutputLineHandler(
            DownloadJob job,
            YtDlpOutputPathParser outputPathParser,
            YtDlpMetadataParser metadataParser,
            Action<DownloadJob, string> applyProgress,
            Action notifyChanged)
        {
            _job = job;
            _outputPathParser = outputPathParser;
            _metadataParser = metadataParser;
            _applyProgress = applyProgress;
            _notifyChanged = notifyChanged;
        }

        public void Report(string line)
        {
            _applyProgress(_job, line);

            if (_outputPathParser.TryAddCandidate(line, _job.OutputPaths))
                _notifyChanged();

            if (_metadataParser.TryApplyMetadata(_job, line))
                _notifyChanged();
        }
    }
}
