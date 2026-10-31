// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TubeGrabber.Core.Services;

/// <summary>
/// Appends <see cref="Trace"/> output to one <c>app-yyyy-MM-dd.log</c> file per day and keeps the newest few files.
/// I/O failures are swallowed: logging must never take the app down.
/// </summary>
public sealed class RollingFileTraceListener : TraceListener
{
    public const string FilePrefix = "app-";
    public const string FileExtension = ".log";
    public const int DefaultRetainedFiles = 7;

    private readonly string _directory;
    private readonly int _retainedFiles;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new();
    private DateTime _lastPruneDay = DateTime.MinValue;

    public RollingFileTraceListener(string directory, int retainedFiles = DefaultRetainedFiles, Func<DateTime>? now = null)
    {
        _directory = directory;
        _retainedFiles = retainedFiles;
        _now = now ?? (() => DateTime.Now);
    }

    public static string FileNameFor(DateTime day) =>
        FilePrefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + FileExtension;

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
        Append(LevelOf(eventType), source, message);

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args) =>
        Append(LevelOf(eventType), source, args is { Length: > 0 } && format is not null
            ? string.Format(CultureInfo.InvariantCulture, format, args)
            : format);

    public override void Write(string? message) => Append("Info", null, message);

    public override void WriteLine(string? message) => Append("Info", null, message);

    public void LogException(string source, Exception ex) => Append("Critical", source, ex.ToString());

    private static string LevelOf(TraceEventType type) => type switch
    {
        TraceEventType.Critical => "Critical",
        TraceEventType.Error => "Error",
        TraceEventType.Warning => "Warning",
        _ => "Info",
    };

    private void Append(string level, string? source, string? message)
    {
        try
        {
            lock (_gate)
            {
                var now = _now();
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, FileNameFor(now));
                var isNew = !File.Exists(path);
                var line = $"{now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {source ?? "-"}: {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);

                if (isNew)
                {
                    ConfigFilePermissions.ApplyRestrictedPermissions(_directory, isDirectory: true);
                    ConfigFilePermissions.ApplyRestrictedPermissions(path, isDirectory: false);
                }

                if (_lastPruneDay != now.Date)
                {
                    _lastPruneDay = now.Date;
                    Prune();
                }
            }
        }
        catch
        {
            // Read-only disk, full disk, permission problems: drop the entry.
        }
    }

    private void Prune()
    {
        // Date-stamped names sort chronologically.
        var stale = Directory.EnumerateFiles(_directory, FilePrefix + "*" + FileExtension)
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(_retainedFiles);
        foreach (var file in stale)
        {
            try { File.Delete(file); }
            catch { /* in use or read-only; retry next day */ }
        }
    }
}
