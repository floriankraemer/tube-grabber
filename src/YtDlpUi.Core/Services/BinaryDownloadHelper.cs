// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using YtDlpUi.Core.Models;

namespace YtDlpUi.Core.Services;

public sealed class BinaryDownloadHelper
{
    private readonly HttpClient _httpClient;

    public BinaryDownloadHelper(HttpClient? httpClient = null) =>
        _httpClient = httpClient ?? new HttpClient();

    public async Task DownloadAsync(ReleaseAsset asset, string destinationPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = destinationPath + ".download";
        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var file = File.Create(tempPath))
            await stream.CopyToAsync(file, cancellationToken);

        var fileInfo = new FileInfo(tempPath);
        if (fileInfo.Length < asset.MinimumSizeBytes)
            throw new InvalidOperationException("Downloaded file is smaller than expected.");

        if (!string.IsNullOrWhiteSpace(asset.Sha256))
        {
            var hash = await ComputeSha256Async(tempPath, cancellationToken);
            if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Checksum verification failed.");
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }
        catch
        {
            // Best effort.
        }
    }

    public static string ExtractFfmpegFromArchive(string archivePath, string extractDirectory)
    {
        Directory.CreateDirectory(extractDirectory);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractDirectory, overwriteFiles: true);
        }
        else if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            ExtractTarXzWithProcess(archivePath, extractDirectory);

        // Match the exact executable name: the downloaded archive lives in this directory too.
        var executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var ffmpeg = Directory.EnumerateFiles(extractDirectory, executableName, SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), executableName, StringComparison.OrdinalIgnoreCase));

        if (ffmpeg is null)
            throw new FileNotFoundException("ffmpeg binary not found in archive.");

        var target = Path.Combine(extractDirectory, executableName);
        if (!string.Equals(Path.GetFullPath(ffmpeg), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            File.Copy(ffmpeg, target, overwrite: true);

        MakeExecutable(target);
        return target;
    }

    public static string ExtractDenoFromArchive(string archivePath, string extractDirectory)
    {
        Directory.CreateDirectory(extractDirectory);

        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Deno release archive must be a zip file.");

        ZipFile.ExtractToDirectory(archivePath, extractDirectory, overwriteFiles: true);

        var executableName = OperatingSystem.IsWindows() ? "deno.exe" : "deno";
        var deno = Directory.EnumerateFiles(extractDirectory, executableName, SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), executableName, StringComparison.OrdinalIgnoreCase));

        if (deno is null)
            throw new FileNotFoundException("deno binary not found in archive.");

        var target = Path.Combine(extractDirectory, executableName);
        if (!string.Equals(Path.GetFullPath(deno), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            File.Copy(deno, target, overwrite: true);

        MakeExecutable(target);
        return target;
    }

    private static void ExtractTarXzWithProcess(string archivePath, string extractDirectory)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xf \"{archivePath}\" -C \"{extractDirectory}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start tar for extraction.");

        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
    }
}
