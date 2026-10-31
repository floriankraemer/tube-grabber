// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpBinaryInstaller : IBinaryInstaller
{
    private readonly IBinaryReleaseSource _releaseSource;
    private readonly IBinaryLocator _binaryLocator;
    private readonly BinaryDownloadHelper _downloadHelper;
    private readonly string _runtimeIdentifier;

    public YtDlpBinaryInstaller(
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
        try
        {
            asset = _releaseSource.GetYtDlpAsset(_runtimeIdentifier);
            var destination = _binaryLocator.GetBundledYtDlpPath();
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            await _downloadHelper.DownloadAsync(asset, destination, cancellationToken);
            BinaryDownloadHelper.MakeExecutable(destination);

            return BinaryInstallResult.Success(destination);
        }
        catch (UnsupportedPlatformException ex)
        {
            return BinaryInstallResult.Failure(
                BinaryInstallMessages.FormatUnsupportedPlatform("yt-dlp", ex.RuntimeIdentifier, ex.ReleasePageUrl),
                ex.ReleasePageUrl);
        }
        catch (Exception ex)
        {
            var existing = _binaryLocator.GetBundledYtDlpPath();
            if (File.Exists(existing))
                return BinaryInstallResult.Success(existing);

            var releasePageUrl = asset?.ReleasePageUrl ?? BinaryReleaseManifest.YtDlpReleasePageUrl;
            return BinaryInstallResult.Failure(
                BinaryInstallMessages.FormatFailure("yt-dlp", _runtimeIdentifier, ex.Message, releasePageUrl),
                releasePageUrl);
        }
    }
}
