// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TubeGrabber.UI.Services;

public sealed class StoragePickerService
{
    public async Task<string?> PickFolderAsync(TopLevel owner, string title = "Select download folder")
    {
        var storageProvider = GetStorageProvider(owner);
        if (storageProvider is null)
            return null;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        if (folders.Count == 0)
            return null;

        return folders[0].TryGetLocalPath();
    }

    public async Task<string?> PickExecutableAsync(TopLevel owner, string title = "Select executable")
    {
        var storageProvider = GetStorageProvider(owner);
        if (storageProvider is null)
            return null;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = GetExecutableFileTypes(),
        });

        if (files.Count == 0)
            return null;

        return files[0].TryGetLocalPath();
    }

    private static IStorageProvider? GetStorageProvider(TopLevel owner) =>
        owner.StorageProvider;

    private static List<FilePickerFileType> GetExecutableFileTypes()
    {
        if (OperatingSystem.IsWindows())
        {
            return
            [
                new FilePickerFileType("Executable") { Patterns = ["*.exe", "*.bat", "*.cmd"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ];
        }

        return [new FilePickerFileType("All files") { Patterns = ["*"] }];
    }
}
