// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Diagnostics;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class RollingFileTraceListenerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"tube-grabber-log-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void TraceEvent_WritesLevelAndMessageToDailyFile()
    {
        var day = new DateTime(2026, 3, 5, 10, 0, 0);
        using var listener = new RollingFileTraceListener(_dir, now: () => day);

        listener.TraceEvent(null, "src", TraceEventType.Warning, 0, "boom {0}", new object[] { new InvalidOperationException("x") });

        var text = File.ReadAllText(Path.Combine(_dir, "app-2026-03-05.log"));
        Assert.Contains("[Warning]", text);
        Assert.Contains("boom System.InvalidOperationException: x", text);
    }

    [Fact]
    public void Write_KeepsOnlyNewestFiles()
    {
        var day = new DateTime(2026, 3, 1);
        using var listener = new RollingFileTraceListener(_dir, retainedFiles: 3, now: () => day);
        for (var i = 0; i < 6; i++)
        {
            day = new DateTime(2026, 3, 1).AddDays(i);
            listener.WriteLine("hi");
        }

        var names = Directory.GetFiles(_dir).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(new[] { "app-2026-03-04.log", "app-2026-03-05.log", "app-2026-03-06.log" }, names);
    }

    [Fact]
    public void Write_WhenDirectoryIsAFile_DoesNotThrow()
    {
        File.WriteAllText(_dir, "not a directory");
        try
        {
            using var listener = new RollingFileTraceListener(_dir);
            listener.WriteLine("hi");
        }
        finally
        {
            File.Delete(_dir);
        }
    }
}
