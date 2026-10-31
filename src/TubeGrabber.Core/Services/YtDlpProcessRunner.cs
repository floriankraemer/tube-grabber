// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using System.Text;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpProcessRunner : IYtDlpProcessRunner
{
    public async Task<YtDlpRunResult> RunAsync(
        YtDlpInvocation invocation,
        IProgress<string>? stdoutProgress = null,
        CancellationToken cancellationToken = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = invocation.ExecutablePath,
                WorkingDirectory = invocation.WorkingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var arg in invocation.Arguments)
            process.StartInfo.ArgumentList.Add(arg);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;

            stdout.AppendLine(e.Data);
            stdoutProgress?.Report(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;

            stderr.AppendLine(e.Data);
            stdoutProgress?.Report(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill failures.
            }

            return new YtDlpRunResult
            {
                ExitCode = -1,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
                WasCancelled = true,
            };
        }

        return new YtDlpRunResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
        };
    }

    public static Task<string?> GetVersionAsync(string executablePath, CancellationToken cancellationToken = default) =>
        GetFirstOutputLineAsync(executablePath, ["--version"], cancellationToken);

    public static async Task<string?> GetFirstOutputLineAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var invocation = new YtDlpInvocation
        {
            ExecutablePath = executablePath,
            Arguments = arguments,
        };

        var runner = new YtDlpProcessRunner();
        var result = await runner.RunAsync(invocation, cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            return null;

        var output = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
    }
}
