// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.Services;

/// <summary>
/// Runs commands one at a time, in order, on a thread-pool worker. A command with a coalesce key replaces a
/// still-pending command with the same key, so only the latest of a burst (e.g. rapid seeks) runs.
/// <see cref="Close"/> drops everything pending, runs one last command and stops accepting new ones.
/// </summary>
public sealed class SerialCommandQueue
{
    private readonly object _gate = new();
    private readonly List<(string? Key, Action Command)> _pending = new();
    private Task _worker = Task.CompletedTask;
    private bool _running;
    private bool _closed;

    /// <summary>Queues a command; returns false once the queue is closed.</summary>
    public bool Enqueue(Action command, string? coalesceKey = null)
    {
        lock (_gate)
        {
            if (_closed)
                return false;

            Add(command, coalesceKey);
            EnsureWorker();
            return true;
        }
    }

    /// <summary>Discards pending commands, queues <paramref name="last"/>, and returns a task done once it ran.</summary>
    public Task Close(Action last)
    {
        lock (_gate)
        {
            if (_closed)
                return _worker;

            _closed = true;
            _pending.Clear();
            Add(last, null);
            EnsureWorker();
            return _worker;
        }
    }

    private void Add(Action command, string? key)
    {
        var index = key is null ? -1 : _pending.FindIndex(p => p.Key == key);
        if (index >= 0)
            _pending[index] = (key, command);
        else
            _pending.Add((key, command));
    }

    private void EnsureWorker()
    {
        if (_running)
            return;

        _running = true;
        _worker = Task.Run(Drain);
    }

    private void Drain()
    {
        while (true)
        {
            Action command;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _running = false;
                    return;
                }

                command = _pending[0].Command;
                _pending.RemoveAt(0);
            }

            try
            {
                command();
            }
            catch (Exception ex)
            {
                // One failing libvlc call must not stop the commands behind it (notably the final teardown).
                System.Diagnostics.Trace.TraceError($"Queued command failed: {ex}");
            }
        }
    }
}
