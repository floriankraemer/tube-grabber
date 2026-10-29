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
        NamedPipeServerStream? server = null;
        while (!token.IsCancellationRequested)
        {
            try
            {
                server ??= CreateServer();
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                // On Unix all instances share one listening socket that closes with the last instance;
                // create the next one first so clients queued meanwhile are not dropped.
                var connected = server;
                server = CreateServer();
                await using (connected.ConfigureAwait(false))
                {
                    using var reader = new StreamReader(connected);
                    var message = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (message == ShowMessage)
                        onShow();
                }
            }
            catch (OperationCanceledException)
            {
                break;
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
                    break;
                }
            }
        }

        if (server is not null)
            await server.DisposeAsync().ConfigureAwait(false);
    }

    private NamedPipeServerStream CreateServer() =>
        new(
            _pipeName,
            PipeDirection.In,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static string Sanitize(string value) =>
        new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
}
