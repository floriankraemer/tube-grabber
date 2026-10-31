// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json.Nodes;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Services;

namespace TubeGrabber.Core.Tests;

public sealed class ConfigFolderMigrationTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("tube-grabber-migration-").FullName;

    private string Legacy => Path.Combine(_home, AppPaths.LegacyConfigFolderName);

    private string Current => Path.Combine(_home, AppPaths.ConfigFolderName);

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Migrate_WhenOnlyLegacyExists_CopiesEverythingAndKeepsLegacy()
    {
        Write(Legacy, "app.json", "{}");
        Write(Legacy, "profiles/default.json", "{\"Id\":\"default\"}");
        Write(Legacy, "bin/yt-dlp/yt-dlp", "binary");

        var result = ConfigFolderMigration.Migrate(_home);

        Assert.Equal(ConfigMigrationOutcome.Migrated, result.Outcome);
        Assert.Equal("binary", File.ReadAllText(Path.Combine(Current, "bin", "yt-dlp", "yt-dlp")));
        Assert.True(File.Exists(Path.Combine(Current, "profiles", "default.json")));
        Assert.True(File.Exists(Path.Combine(Legacy, "app.json")));
        Assert.Empty(Directory.GetDirectories(_home, "*migrating*"));
    }

    [Fact]
    public void Migrate_SkipsInstanceLock()
    {
        Write(Legacy, "app.json", "{}");
        Write(Legacy, "instance.lock", "");

        ConfigFolderMigration.Migrate(_home);

        Assert.False(File.Exists(Path.Combine(Current, "instance.lock")));
    }

    [Fact]
    public void Migrate_WhenNewFolderExists_LeavesBothUntouched()
    {
        Write(Legacy, "app.json", "legacy");
        Write(Current, "app.json", "current");

        var result = ConfigFolderMigration.Migrate(_home);

        Assert.Equal(ConfigMigrationOutcome.NotNeeded, result.Outcome);
        Assert.Equal("current", File.ReadAllText(Path.Combine(Current, "app.json")));
        Assert.Equal("legacy", File.ReadAllText(Path.Combine(Legacy, "app.json")));
    }

    [Fact]
    public void Migrate_WhenNeitherExists_DoesNothing()
    {
        var result = ConfigFolderMigration.Migrate(_home);

        Assert.Equal(ConfigMigrationOutcome.NotNeeded, result.Outcome);
        Assert.False(Directory.Exists(Current));
    }

    [Fact]
    public void Migrate_WhenCopyFailsMidway_LeavesNoPartialFolderAndCanRetry()
    {
        Write(Legacy, "app.json", "{}");
        Write(Legacy, "profiles/default.json", "{}");
        var copied = 0;

        var failed = ConfigFolderMigration.Migrate(_home, _ =>
        {
            if (++copied == 2)
                throw new IOException("disk full");
        });

        Assert.Equal(ConfigMigrationOutcome.Failed, failed.Outcome);
        Assert.IsType<IOException>(failed.Error);
        Assert.False(Directory.Exists(Current));
        Assert.Empty(Directory.GetDirectories(_home, "*migrating*"));
        Assert.Equal(ConfigMigrationOutcome.Migrated, ConfigFolderMigration.Migrate(_home).Outcome);
    }

    [Fact]
    public void Migrate_ReplacesStaleTempFolderFromCrashedRun()
    {
        Write(Legacy, "app.json", "{}");
        Write(_home, ".tube-grabber.migrating/stale.txt", "x");

        var result = ConfigFolderMigration.Migrate(_home);

        Assert.Equal(ConfigMigrationOutcome.Migrated, result.Outcome);
        Assert.False(File.Exists(Path.Combine(Current, "stale.txt")));
    }

    [Fact]
    public void Migrate_RewritesAbsolutePathsIntoLegacyFolder()
    {
        var legacyBinary = Path.Combine(Legacy, "bin", "yt-dlp", "yt-dlp");
        var config = new JsonObject
        {
            ["YtDlpPath"] = legacyBinary,
            ["FfmpegPath"] = "/usr/bin/ffmpeg",
            ["DownloadFolder"] = Legacy + "-old/downloads",
            ["Nested"] = new JsonArray(Legacy, "keep"),
        };
        var originalJson = config.ToJsonString();
        Write(Legacy, "app.json", originalJson);
        Write(Legacy, "profiles/custom.json", new JsonObject { ["Out"] = Path.Combine(Legacy, "x") }.ToJsonString());
        Write(Legacy, "broken.json", "{not json");

        ConfigFolderMigration.Migrate(_home);

        var migrated = JsonNode.Parse(File.ReadAllText(Path.Combine(Current, "app.json")))!;
        Assert.Equal(Path.Combine(Current, "bin", "yt-dlp", "yt-dlp"), (string?)migrated["YtDlpPath"]);
        Assert.Equal("/usr/bin/ffmpeg", (string?)migrated["FfmpegPath"]);
        Assert.Equal(Legacy + "-old/downloads", (string?)migrated["DownloadFolder"]);
        Assert.Equal(Current, (string?)migrated["Nested"]![0]);
        Assert.Equal("keep", (string?)migrated["Nested"]![1]);
        Assert.Equal(Path.Combine(Current, "x"), (string?)JsonNode.Parse(File.ReadAllText(Path.Combine(Current, "profiles", "custom.json")))!["Out"]);
        Assert.Equal("{not json", File.ReadAllText(Path.Combine(Current, "broken.json")));
        Assert.Equal(originalJson, File.ReadAllText(Path.Combine(Legacy, "app.json")));
    }

    [Fact]
    public void Migrate_PreservesUnixFileModes()
    {
        if (OperatingSystem.IsWindows())
            return;

        Write(Legacy, "app.json", "{}");
        File.SetUnixFileMode(Path.Combine(Legacy, "app.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Write(Legacy, "bin/tool", "x");
        File.SetUnixFileMode(Path.Combine(Legacy, "bin", "tool"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead);

        ConfigFolderMigration.Migrate(_home);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(Current, "app.json")));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead,
            File.GetUnixFileMode(Path.Combine(Current, "bin", "tool")));
    }
}
