// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class FfmpegBinaryInstaller : IBinaryInstaller
{
    private readonly IBinaryReleaseSource _releaseSource;
    private readonly IBinaryLocator _binaryLocator;
    private readonly BinaryDownloadHelper _downloadHelper;
    private readonly string _runtimeIdentifier;

    public FfmpegBinaryInstaller(
        IBinaryReleaseSource releaseSource,
        IBinaryLocator binaryLocator,
        BinaryDownloadHelper downloadHelper,
        string? runtimeIdentifier = null)
    {
        _releaseSource = releaseSource;
        _binaryLocator = binaryLocator;
        _downloadHelper = downloadHelper;
        _runtimeIdentifier = runtimeIdentifier ?? System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
    }

    public async Task<BinaryInstallResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        ReleaseAsset? asset = null;
        var extractDir = Path.Combine(Path.GetDirectoryName(_binaryLocator.GetBundledFfmpegPath())!, "extract");
        try
        {
            asset = _releaseSource.GetFfmpegAsset(_runtimeIdentifier);
            var archivePath = Path.Combine(extractDir, asset.FileName);

            Directory.CreateDirectory(extractDir);
            await _downloadHelper.DownloadAsync(asset, archivePath, cancellationToken);

            var ffmpegPath = BinaryDownloadHelper.ExtractFfmpegFromArchive(archivePath, extractDir);
            var target = _binaryLocator.GetBundledFfmpegPath();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (!string.Equals(Path.GetFullPath(ffmpegPath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                File.Copy(ffmpegPath, target, overwrite: true);

            BinaryDownloadHelper.MakeExecutable(target);
            return BinaryInstallResult.Success(target);
        }
        catch (UnsupportedPlatformException ex)
        {
            return BinaryInstallResult.Failure(
                BinaryInstallMessages.FormatUnsupportedPlatform("ffmpeg", ex.RuntimeIdentifier, ex.ReleasePageUrl),
                ex.ReleasePageUrl);
        }
        catch (Exception ex)
        {
            var existing = _binaryLocator.GetBundledFfmpegPath();
            if (File.Exists(existing))
                return BinaryInstallResult.Success(existing);

            var releasePageUrl = asset?.ReleasePageUrl ?? BinaryReleaseManifest.FfmpegReleasePageUrl;
            return BinaryInstallResult.Failure(
                BinaryInstallMessages.FormatFailure("ffmpeg", _runtimeIdentifier, ex.Message, releasePageUrl),
                releasePageUrl);
        }
        finally
        {
            // The archive and extracted tree are scratch files; only the copied binary is kept.
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, recursive: true);
        }
    }
}
