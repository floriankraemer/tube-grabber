// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

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

    /// <summary>
    /// Copies <c>ffprobe</c> from an extracted ffmpeg archive next to the installed ffmpeg. yt-dlp needs it for
    /// post-processing that reads durations (SponsorBlock removal, chapters). Returns false when the archive has none.
    /// </summary>
    public static bool CopyFfprobe(string extractDirectory, string targetDirectory)
    {
        var name = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        var source = Directory.EnumerateFiles(extractDirectory, name, SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
        if (source is null)
            return false;

        var target = Path.Combine(targetDirectory, name);
        File.Copy(source, target, overwrite: true);
        MakeExecutable(target);
        return true;
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

        const string Hint = "Unpacking a .tar.xz archive needs the 'tar' and 'xz' tools; install them with your package manager (for example the xz-utils package) and try again";

        System.Diagnostics.Process process;
        try
        {
            process = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start tar. {Hint}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"tar was not found. {Hint}", ex);
        }

        using (process)
        {
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var reason = stderr.GetAwaiter().GetResult().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "tar failed";
                throw new InvalidOperationException($"{reason}. {Hint}");
            }
        }
    }
}
