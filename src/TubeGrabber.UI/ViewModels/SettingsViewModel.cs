// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using TubeGrabber.Core.Abstractions;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;
using TubeGrabber.UI.Services;

namespace TubeGrabber.UI.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsCoordinator _coordinator;
    private readonly DownloadFolderService _downloadFolderService;
    private readonly IBinaryInstaller _ytDlpInstaller;
    private readonly IBinaryInstaller _ffmpegInstaller;
    private readonly IBinaryInstaller _denoInstaller;
    private readonly BinaryInstallService _binaryInstallService;
    private readonly IFileSystemLauncher _fileSystemLauncher;
    private readonly ILoginItemService _loginItemService;

    private AppConfiguration _config = new();
    private string? _validationError;
    private string? _statusMessage;
    private JsRuntimeEngineDefinition _selectedJsRuntime = JsRuntimeEngines.All[0];
    private bool _isInstalling;
    private string? _validationProfileId;
    private string _configSnapshot = string.Empty;

    public SettingsViewModel(
        SettingsCoordinator coordinator,
        ProfilesViewModel profiles,
        DownloadFolderService downloadFolderService,
        IBinaryInstaller ytDlpInstaller,
        IBinaryInstaller ffmpegInstaller,
        IBinaryInstaller denoInstaller,
        BinaryInstallService binaryInstallService,
        IFileSystemLauncher fileSystemLauncher,
        ILoginItemService loginItemService)
    {
        _coordinator = coordinator;
        _downloadFolderService = downloadFolderService;
        _ytDlpInstaller = ytDlpInstaller;
        _ffmpegInstaller = ffmpegInstaller;
        _denoInstaller = denoInstaller;
        _binaryInstallService = binaryInstallService;
        _fileSystemLauncher = fileSystemLauncher;
        _loginItemService = loginItemService;
        Profiles = profiles;
        Profiles.DirtyStateChanged += (_, _) => RefreshUnsavedState();
        Profiles.OperationFailed += (_, message) => ValidationError = message;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(HasUnsavedChanges) or nameof(UnsavedSummary) or nameof(HasUnsavedSummary)
                or nameof(ShowUnsavedSummary) or nameof(HasValidationError) or nameof(ShowStatusMessage)))
                RefreshUnsavedState();
        };
        YtDlpStatus = new ToolStatusViewModel();
        FfmpegStatus = new ToolStatusViewModel();
        JsRuntimeStatus = new ToolStatusViewModel();
    }

    /// <summary>Last "Test" outcome per tool, shown on the matching binary card.</summary>
    public ToolStatusViewModel YtDlpStatus { get; }
    public ToolStatusViewModel FfmpegStatus { get; }
    public ToolStatusViewModel JsRuntimeStatus { get; }

    /// <summary>The Profiles tab: profile list, detail page and drafts.</summary>
    public ProfilesViewModel Profiles { get; }

    /// <summary>Raised after a successful save, once the settings are on disk.</summary>
    public event EventHandler? Saved;

    public IReadOnlyList<JsRuntimeEngineDefinition> JsRuntimeEngineOptions => JsRuntimeEngines.All;
    public IReadOnlyList<ThemePreference> ThemePreferenceOptions { get; } =
        [ThemePreference.System, ThemePreference.Light, ThemePreference.Dark];
    public AppConfiguration Config
    {
        get => _config;
        private set => SetProperty(ref _config, value);
    }

    public string? ValidationError
    {
        get => _validationError;
        set => SetProperty(ref _validationError, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set => SetProperty(ref _isInstalling, value);
    }

    public ThemePreference ThemePreference
    {
        get => Config.ThemePreference;
        set
        {
            if (Config.ThemePreference == value)
                return;

            Config.ThemePreference = value;
            OnPropertyChanged();
            ThemeService.Apply(value);
        }
    }

    public int MaxConcurrentDownloads
    {
        get => Config.MaxConcurrentDownloads;
        set
        {
            Config.MaxConcurrentDownloads = value;
            OnPropertyChanged();
        }
    }

    public bool RunInBackground
    {
        get => Config.RunInBackground;
        set
        {
            Config.RunInBackground = value;
            OnPropertyChanged();
        }
    }

    public bool StartAtLogin
    {
        get => Config.StartAtLogin;
        set
        {
            Config.StartAtLogin = value;
            OnPropertyChanged();
        }
    }

    public int SubscriptionCheckIntervalMinutes
    {
        get => Config.SubscriptionCheckIntervalMinutes;
        set
        {
            Config.SubscriptionCheckIntervalMinutes = value;
            OnPropertyChanged();
        }
    }

    public string? DownloadFolder
    {
        get => Config.DownloadFolder ?? string.Empty;
        set
        {
            Config.DownloadFolder = value;
            OnPropertyChanged();
        }
    }

    public string? YtDlpPath
    {
        get => Config.YtDlpPath ?? string.Empty;
        set
        {
            Config.YtDlpPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResolvedYtDlpPath));
            Profiles.RefreshPreview();
        }
    }

    public string? FfmpegPath
    {
        get => Config.FfmpegPath ?? string.Empty;
        set
        {
            Config.FfmpegPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResolvedFfmpegPath));
            Profiles.RefreshPreview();
        }
    }

    public JsRuntimeEngineDefinition SelectedJsRuntimeEngine
    {
        get => _selectedJsRuntime;
        set
        {
            if (!SetProperty(ref _selectedJsRuntime, value))
                return;

            Config.JsRuntimeEngine = string.IsNullOrEmpty(value.Id) ? null : value.Id;
            OnPropertyChanged(nameof(JsRuntimeDescription));
            OnPropertyChanged(nameof(IsJsRuntimePathEnabled));
            OnPropertyChanged(nameof(IsDenoInstallEnabled));
            OnPropertyChanged(nameof(ResolvedJsRuntimePath));
            OnPropertyChanged(nameof(JsRuntimesArgumentPreview));
            Profiles.RefreshPreview();
        }
    }

    public string? JsRuntimePath
    {
        get => Config.JsRuntimePath ?? string.Empty;
        set
        {
            Config.JsRuntimePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResolvedJsRuntimePath));
            OnPropertyChanged(nameof(JsRuntimesArgumentPreview));
            Profiles.RefreshPreview();
        }
    }

    public string YtDlpPathPlaceholder => PathPlaceholder("yt-dlp");
    public string FfmpegPathPlaceholder => PathPlaceholder("ffmpeg");
    public string JsRuntimePathPlaceholder =>
        $"Optional path to runtime executable (e.g. {PathPlaceholder("deno")})";
    public string DownloadFolderPlaceholder => OperatingSystem.IsWindows()
        ? @"C:\Users\you\youtube-downloads"
        : "/home/you/youtube-downloads";

    private static string PathPlaceholder(string tool) => OperatingSystem.IsWindows()
        ? $@"C:\path\to\{tool}.exe"
        : $"/usr/bin/{tool}";

    public string JsRuntimeDescription => SelectedJsRuntimeEngine.Description;

    public bool IsJsRuntimePathEnabled => !string.IsNullOrEmpty(SelectedJsRuntimeEngine.Id);

    public bool IsDenoInstallEnabled =>
        string.Equals(SelectedJsRuntimeEngine.Id, JsRuntimeEngines.Deno, StringComparison.OrdinalIgnoreCase);

    public string ResolvedJsRuntimePath =>
        IsJsRuntimePathEnabled
            ? _coordinator.ResolveJsRuntimePath(Config) ?? "Not found"
            : "Not configured";

    public string JsRuntimesArgumentPreview =>
        _coordinator.BuildJsRuntimesArgument(Config) ?? "(not passed to yt-dlp)";

    public async Task LoadAsync()
    {
        var (config, profiles) = await _coordinator.LoadAsync();
        Config = config;

        if (string.IsNullOrWhiteSpace(Config.YtDlpPath))
            Config.YtDlpPath = _coordinator.ResolveYtDlpPath(Config);

        if (string.IsNullOrWhiteSpace(Config.FfmpegPath))
            Config.FfmpegPath = _coordinator.ResolveFfmpegPath(Config);

        if (string.IsNullOrWhiteSpace(Config.JsRuntimePath))
            Config.JsRuntimePath = _coordinator.ResolveJsRuntimePath(Config);

        await Profiles.LoadAsync(Config, profiles);
        _selectedJsRuntime = JsRuntimeEngines.Find(config.JsRuntimeEngine) ?? JsRuntimeEngines.All[0];
        OnPropertyChanged(nameof(SelectedJsRuntimeEngine));
        OnPropertyChanged(nameof(JsRuntimeDescription));
        OnPropertyChanged(nameof(IsJsRuntimePathEnabled));
        OnPropertyChanged(nameof(IsDenoInstallEnabled));
        OnPropertyChanged(nameof(ResolvedYtDlpPath));
        OnPropertyChanged(nameof(ResolvedFfmpegPath));
        OnPropertyChanged(nameof(ResolvedJsRuntimePath));
        OnPropertyChanged(nameof(JsRuntimesArgumentPreview));
        OnPropertyChanged(nameof(ThemePreference));
        OnPropertyChanged(nameof(RunInBackground));
        OnPropertyChanged(nameof(StartAtLogin));
        OnPropertyChanged(nameof(SubscriptionCheckIntervalMinutes));
        OnPropertyChanged(nameof(YtDlpPath));
        OnPropertyChanged(nameof(FfmpegPath));
        OnPropertyChanged(nameof(JsRuntimePath));

        // Discard reloads the saved theme too; it was applied while it was edited.
        ThemeService.Apply(Config.ThemePreference);
        TakeConfigSnapshot();
        ValidationError = null;
        ValidationProfileId = null;
        RefreshUnsavedState();
    }

    public Task InstallYtDlpAsync() =>
        RunInstallAsync(ManagedBinary.YtDlp, _ytDlpInstaller, "yt-dlp");

    public Task InstallFfmpegAsync() =>
        RunInstallAsync(ManagedBinary.Ffmpeg, _ffmpegInstaller, "ffmpeg");

    public Task InstallDenoAsync() =>
        RunInstallAsync(ManagedBinary.Deno, _denoInstaller, "Deno");

    public void OpenYtDlpReleasesPage() => OpenReleasesPage(ManagedBinary.YtDlp);

    public void OpenFfmpegReleasesPage() => OpenReleasesPage(ManagedBinary.Ffmpeg);

    public void OpenDenoReleasesPage() => OpenReleasesPage(ManagedBinary.Deno);

    /// <summary>Id of the first profile with a validation error; the footer opens it when the error is clicked.</summary>
    public string? ValidationProfileId
    {
        get => _validationProfileId;
        private set => SetProperty(ref _validationProfileId, value);
    }

    /// <summary>True while a profile draft or any other setting differs from what is saved.</summary>
    public bool HasUnsavedChanges => Profiles.DirtyProfileCount > 0 || HasOtherUnsavedChanges;

    private bool HasOtherUnsavedChanges => _configSnapshot.Length > 0 && SnapshotConfig() != _configSnapshot;

    /// <summary>Footer text while something is unsaved; empty otherwise.</summary>
    public string UnsavedSummary =>
        Profiles.DirtyProfileCount > 0 ? Profiles.UnsavedSummary : HasOtherUnsavedChanges ? "Unsaved changes" : string.Empty;

    public bool HasUnsavedSummary => UnsavedSummary.Length > 0;
    public bool ShowUnsavedSummary => HasUnsavedSummary && !HasValidationError;
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);
    public bool ShowStatusMessage => !HasUnsavedSummary && !HasValidationError && !string.IsNullOrEmpty(StatusMessage);

    public async Task<bool> SaveAsync()
    {
        var neutral = new DownloadProfile { Id = "settings", Name = "Settings" };
        var settingsErrors = _coordinator.Validate(Config, neutral);
        var profileErrors = Profiles.ValidateDirty(settingsErrors);
        if (settingsErrors.Count + profileErrors.Count > 0)
        {
            ValidationError = string.Join(Environment.NewLine, settingsErrors.Concat(profileErrors.Select(e => e.Message)));
            ValidationProfileId = profileErrors.Count > 0 ? profileErrors[0].ProfileId : null;
            return false;
        }

        if (!_downloadFolderService.IsConfigured(Config))
        {
            ValidationError = "Download folder is required.";
            ValidationProfileId = null;
            return false;
        }

        if (_downloadFolderService.TryNormalize(Config.DownloadFolder, out var normalized))
        {
            if (!_downloadFolderService.EnsureExists(normalized))
            {
                ValidationError = "Download folder path is invalid or cannot be created.";
                ValidationProfileId = null;
                return false;
            }

            Config.DownloadFolder = normalized;
        }

        ValidationError = null;
        ValidationProfileId = null;
        await _coordinator.SaveAsync(Config, Profiles.GetDirtyDrafts());
        Profiles.MarkAllSaved();
        TakeConfigSnapshot();
        ThemeService.Apply(Config.ThemePreference);

        try
        {
            SyncStartAtLogin();
        }
        catch (Exception ex)
        {
            ValidationError = $"Settings saved, but start at login could not be updated: {ex.Message}";
            return false;
        }
        finally
        {
            RefreshUnsavedState();
        }

        StatusMessage = "Settings saved.";
        Saved?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Decides whether the user may leave the Settings page. With unsaved changes the user is asked first:
    /// Save leaves only if saving worked, Discard drops the edits, Cancel stays.
    /// </summary>
    public async Task<bool> ConfirmLeaveAsync(Func<Task<UnsavedChangesChoice>> ask)
    {
        if (!HasUnsavedChanges)
            return true;

        switch (await ask())
        {
            case UnsavedChangesChoice.Save:
                return await SaveAsync();
            case UnsavedChangesChoice.Discard:
                await LoadAsync();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Opens the profile named by the last validation error, if there is one.</summary>
    public void OpenFailingProfile()
    {
        if (ValidationProfileId is { } id)
            Profiles.OpenProfile(id);
    }

    private void RefreshUnsavedState()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedSummary));
        OnPropertyChanged(nameof(HasUnsavedSummary));
        OnPropertyChanged(nameof(ShowUnsavedSummary));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(ShowStatusMessage));
    }

    private string SnapshotConfig() => System.Text.Json.JsonSerializer.Serialize(Config);

    private void TakeConfigSnapshot() => _configSnapshot = SnapshotConfig();

    public Task<string?> TestYtDlpAsync() => _coordinator.TestYtDlpAsync(Config);

    public Task<string?> TestFfmpegAsync() => _coordinator.TestFfmpegAsync(Config);

    public Task<string?> TestJsRuntimeAsync() => _coordinator.TestJsRuntimeAsync(Config);

    public string ResolvedYtDlpPath => _coordinator.ResolveYtDlpPath(Config) ?? "Not found";

    public string ResolvedFfmpegPath => _coordinator.ResolveFfmpegPath(Config) ?? "Not found";

    public void ApplyTestResult(string toolName, string? result)
    {
        var card = toolName switch
        {
            "yt-dlp" => YtDlpStatus,
            "ffmpeg" => FfmpegStatus,
            _ => JsRuntimeStatus,
        };

        if (string.IsNullOrWhiteSpace(result))
        {
            card.Set("Test failed.", isError: true);
            ValidationError = $"{toolName} test failed.";
            StatusMessage = null;
            return;
        }

        if (result.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || result.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            card.Set(result, isError: true);
            ValidationError = result;
            StatusMessage = null;
            return;
        }

        card.Set(result, isError: false);
        ValidationError = null;
        StatusMessage = $"{toolName}: {result}";
        OnPropertyChanged(nameof(ResolvedYtDlpPath));
        OnPropertyChanged(nameof(ResolvedFfmpegPath));
        OnPropertyChanged(nameof(ResolvedJsRuntimePath));
        OnPropertyChanged(nameof(JsRuntimesArgumentPreview));
    }

    private async Task RunInstallAsync(ManagedBinary binary, IBinaryInstaller installer, string displayName)
    {
        IsInstalling = true;
        ValidationError = null;
        StatusMessage = $"Installing {displayName}...";

        try
        {
            var result = await _binaryInstallService.InstallAsync(binary, installer);
            if (!result.IsSuccess)
            {
                ValidationError = result.Error ?? $"Failed to install {displayName}.";
                StatusMessage = null;
                HandleInstallFailure(result);
                return;
            }

            var (config, _) = await _coordinator.LoadAsync();
            Config = config;

            switch (binary)
            {
                case ManagedBinary.YtDlp:
                    YtDlpPath = config.YtDlpPath ?? string.Empty;
                    break;
                case ManagedBinary.Ffmpeg:
                    FfmpegPath = config.FfmpegPath ?? string.Empty;
                    break;
                case ManagedBinary.Deno:
                    _selectedJsRuntime = JsRuntimeEngines.Find(JsRuntimeEngines.Deno) ?? JsRuntimeEngines.All[0];
                    OnPropertyChanged(nameof(SelectedJsRuntimeEngine));
                    JsRuntimePath = config.JsRuntimePath ?? string.Empty;
                    OnPropertyChanged(nameof(ResolvedJsRuntimePath));
                    OnPropertyChanged(nameof(JsRuntimesArgumentPreview));
                    break;
            }

            // The installer saved the new path itself and the reload above replaced the edited copy.
            TakeConfigSnapshot();
            ValidationError = null;
            StatusMessage = $"{displayName} installed.";
        }
        catch (Exception ex)
        {
            ValidationError = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private void SyncStartAtLogin()
    {
        if (Config.StartAtLogin != _loginItemService.IsEnabled())
            _loginItemService.SetEnabled(Config.StartAtLogin);
    }

    private void HandleInstallFailure(BinaryInstallResult result)
    {
        if (string.IsNullOrWhiteSpace(result.ManualDownloadUrl))
            return;

        if (!_fileSystemLauncher.TryOpenUrl(result.ManualDownloadUrl))
            ValidationError = $"{ValidationError}{Environment.NewLine}{BinaryInstallMessages.FormatBrowserOpenFailure()}";
    }

    private void OpenReleasesPage(ManagedBinary binary)
    {
        var url = GetReleasePageUrl(binary);
        if (!_fileSystemLauncher.TryOpenUrl(url))
            ValidationError = $"Could not open {url} in your browser.";
    }

    private static string GetReleasePageUrl(ManagedBinary binary) =>
        binary switch
        {
            ManagedBinary.YtDlp => BinaryReleaseManifest.YtDlpReleasePageUrl,
            ManagedBinary.Ffmpeg => BinaryReleaseManifest.FfmpegReleasePageUrl,
            ManagedBinary.Deno => BinaryReleaseManifest.DenoReleasePageUrl,
            _ => BinaryReleaseManifest.YtDlpReleasePageUrl,
        };
}
