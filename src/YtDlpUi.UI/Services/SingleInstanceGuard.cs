// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.IO.Pipes;

namespace YtDlpUi.UI.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    internal const string ShowMessage = "show";
    private const int ConnectTimeoutMs = 2000;

    private readonly string _lockPath;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private FileStream? _lock;

    public SingleInstanceGuard(string lockPath, string pipeName)
    {
        _lockPath = lockPath;
        _pipeName = pipeName;
    }

    public static SingleInstanceGuard CreateDefault(string configRoot) =>
        new(Path.Combine(configRoot, "instance.lock"), $"yt-dlp-ui-{Sanitize(Environment.UserName)}");

    public bool TryAcquire()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_lockPath)!);
        try
        {
            _lock = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ShowMessage);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void StartListening(Action onShow) =>
        _ = Task.Run(() => ListenAsync(onShow, _shutdown.Token));

    public void Dispose()
    {
        _shutdown.Cancel();
        _lock?.Dispose();
        _shutdown.Dispose();
    }

    private async Task ListenAsync(Action onShow, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var message = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (message == ShowMessage)
                    onShow();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Single-instance listener error: {ex}");
                try
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static string Sanitize(string value) =>
        new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
}
