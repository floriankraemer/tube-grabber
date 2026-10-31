// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using System.Text.Json.Nodes;
using TubeGrabber.Core.Constants;

namespace TubeGrabber.Core.Services;

public enum ConfigMigrationOutcome
{
    NotNeeded,
    Migrated,
    Failed,
}

public sealed record ConfigMigrationResult(ConfigMigrationOutcome Outcome, Exception? Error = null);

/// <summary>
/// One-time copy of the legacy <c>~/.yt-dlp-ui</c> folder to <c>~/.tube-grabber</c>.
/// The legacy folder is never modified: an older instance may still use it and it allows a rollback.
/// </summary>
public static class ConfigFolderMigration
{
    private const string TempFolderName = ".tube-grabber.migrating";
    private const string InstanceLockFileName = "instance.lock";

    public static ConfigMigrationResult MigrateFromUserHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        return Migrate(home);
    }

    public static ConfigMigrationResult Migrate(string home) => Migrate(home, onFileCopied: null);

    // ponytail: fixed temp name, so two instances starting at the exact same moment could collide; the loser fails
    // gracefully and the next start finds the finished folder.
    internal static ConfigMigrationResult Migrate(string home, Action<string>? onFileCopied)
    {
        var legacyRoot = Path.Combine(home, AppPaths.LegacyConfigFolderName);
        var newRoot = Path.Combine(home, AppPaths.ConfigFolderName);
        if (Directory.Exists(newRoot) || !Directory.Exists(legacyRoot))
            return new ConfigMigrationResult(ConfigMigrationOutcome.NotNeeded);

        var tempRoot = Path.Combine(home, TempFolderName);
        try
        {
            DeleteQuietly(tempRoot);
            CopyDirectory(legacyRoot, tempRoot, onFileCopied);
            RewriteLegacyPaths(tempRoot, legacyRoot, newRoot);
            Directory.Move(tempRoot, newRoot);
            return new ConfigMigrationResult(ConfigMigrationOutcome.Migrated);
        }
        catch (Exception ex)
        {
            DeleteQuietly(tempRoot);
            return new ConfigMigrationResult(ConfigMigrationOutcome.Failed, ex);
        }
    }

    private static void CopyDirectory(string source, string destination, Action<string>? onFileCopied)
    {
        Directory.CreateDirectory(destination);
        ConfigFilePermissions.ApplyRestrictedPermissions(destination, isDirectory: true);

        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var target = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo directory)
            {
                // Symlinked directories are skipped so a link cycle cannot recurse forever.
                if (directory.LinkTarget is null)
                    CopyDirectory(directory.FullName, target, onFileCopied);
            }
            else if (entry.Name != InstanceLockFileName)
            {
                CopyFile(entry.FullName, target);
                onFileCopied?.Invoke(target);
            }
        }

        CopyUnixMode(source, destination);
    }

    private static void CopyFile(string source, string destination)
    {
        // Share-tolerant read: the legacy instance may still hold its log or state files open.
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
        {
            input.CopyTo(output);
        }

        CopyUnixMode(source, destination);
    }

    private static void CopyUnixMode(string source, string destination)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
    }

    // Absolute paths into the legacy folder (for example the installed yt-dlp binary) must follow the copy.
    private static void RewriteLegacyPaths(string root, string legacyRoot, string newRoot)
    {
        var files = Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Concat(Directory.Exists(Path.Combine(root, AppPaths.ProfilesFolderName))
                ? Directory.EnumerateFiles(Path.Combine(root, AppPaths.ProfilesFolderName), "*.json")
                : []);

        foreach (var file in files)
            RewriteFile(file, legacyRoot, newRoot);
    }

    private static void RewriteFile(string file, string legacyRoot, string newRoot)
    {
        JsonNode? document;
        try
        {
            document = JsonNode.Parse(File.ReadAllText(file));
        }
        catch (JsonException)
        {
            return; // Not ours to repair; keep the copy byte-identical.
        }

        if (document is null || !RewriteNode(document, legacyRoot, newRoot, out var rewritten))
            return;

        // Truncating in place keeps the copied file mode.
        File.WriteAllText(file, rewritten.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool RewriteNode(JsonNode node, string legacyRoot, string newRoot, out JsonNode result)
    {
        result = node;
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                var replaced = ReplacePrefix(text, legacyRoot, newRoot);
                if (replaced is null)
                    return false;
                result = JsonValue.Create(replaced)!;
                return true;

            case JsonObject obj:
                var changed = false;
                foreach (var (key, child) in obj.ToList())
                {
                    if (child is not null && RewriteNode(child, legacyRoot, newRoot, out var newChild))
                    {
                        if (!ReferenceEquals(newChild, child))
                            obj[key] = newChild;
                        changed = true;
                    }
                }

                return changed;

            case JsonArray array:
                var arrayChanged = false;
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is { } child && RewriteNode(child, legacyRoot, newRoot, out var newChild))
                    {
                        if (!ReferenceEquals(newChild, child))
                            array[i] = newChild;
                        arrayChanged = true;
                    }
                }

                return arrayChanged;

            default:
                return false;
        }
    }

    private static string? ReplacePrefix(string value, string legacyRoot, string newRoot)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!value.StartsWith(legacyRoot, comparison))
            return null;

        var rest = value[legacyRoot.Length..];
        if (rest.Length > 0 && rest[0] != '/' && rest[0] != '\\')
            return null; // Sibling such as ".yt-dlp-ui-old", not inside the folder.

        return newRoot + rest;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort; a leftover temp folder is replaced on the next attempt.
        }
    }
}
