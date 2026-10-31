// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class DenoBinaryInstaller : IBinaryInstaller
{
    private readonly IBinaryReleaseSource _releaseSource;
    private readonly IBinaryLocator _binaryLocator;
    private readonly BinaryDownloadHelper _downloadHelper;
    private readonly string _runtimeIdentifier;

    public DenoBinaryInstaller(
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
            asset = _releaseSource.GetDenoAsset(_runtimeIdentifier);
            var extractDir = Path.Combine(Path.GetDirectoryName(_binaryLocator.GetBundledDenoPath())!, "extract");
            var archivePath = Path.Combine(extractDir, asset.FileName);

            Directory.CreateDirectory(extractDir);
            await _downloadHelper.DownloadAsync(asset, archivePath, cancellationToken);

            var denoPath = BinaryDownloadHelper.ExtractDenoFromArchive(archivePath, extractDir);
            var target = _binaryLocator.GetBundledDenoPath();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (!string.Equals(Path.GetFullPath(denoPath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                File.Copy(denoPath, target, overwrite: true);

            BinaryDownloadHelper.MakeExecutable(target);
            return BinaryInstallResult.Success(target);
        }
        catch (UnsupportedPlatformException ex)
        {
            return BinaryInstallResult.Failure(
                BinaryInstallMessages.FormatUnsupportedPlatform("Deno", ex.RuntimeIdentifier, ex.ReleasePageUrl),
                ex.ReleasePageUrl);
        }
        catch (Exception ex)
        {
            var existing = _binaryLocator.GetBundledDenoPath();
            if (File.Exists(existing))
                return BinaryInstallResult.Success(existing);

            var releasePageUrl = asset?.ReleasePageUrl ?? BinaryReleaseManifest.DenoReleasePageUrl;
            return BinaryInstallResult.Failure(
                BinaryInstallMessages.FormatFailure("Deno", _runtimeIdentifier, ex.Message, releasePageUrl),
                releasePageUrl);
        }
    }
}
