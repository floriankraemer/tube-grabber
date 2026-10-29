// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Collections.ObjectModel;
using YtDlpUi.Core.Abstractions;
using YtDlpUi.Core.Constants;
using YtDlpUi.Core.Models;
using YtDlpUi.Core.Services;
using YtDlpUi.UI.Services;

namespace YtDlpUi.UI.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsCoordinator _coordinator;
    private readonly YtDlpOptionCatalog _catalog;
    private readonly IProfileStore _profileStore;
    private readonly DownloadFolderService _downloadFolderService;
    private readonly IBinaryInstaller _ytDlpInstaller;
    private readonly IBinaryInstaller _ffmpegInstaller;
    private readonly IBinaryInstaller _denoInstaller;
    private readonly BinaryInstallService _binaryInstallService;
    private readonly IFileSystemLauncher _fileSystemLauncher;
    private readonly ILoginItemService _loginItemService;

    private AppConfiguration _config = new();
    private DownloadProfile _profile = new() { Id = "default", Name = "Default" };
    private string? _validationError;
    private string? _statusMessage;
    private string _cliPreview = string.Empty;
    private string _newProfileName = string.Empty;
    private JsRuntimeEngineDefinition _selectedJsRuntime = JsRuntimeEngines.All[0];
    private bool _isInstalling;

    public SettingsViewModel(
        SettingsCoordinator coordinator,
        YtDlpOptionCatalog catalog,
        IProfileStore profileStore,
        DownloadFolderService downloadFolderService,
        IBinaryInstaller ytDlpInstaller,
        IBinaryInstaller ffmpegInstaller,
        IBinaryInstaller denoInstaller,
        BinaryInstallService binaryInstallService,
        IFileSystemLauncher fileSystemLauncher,
        ILoginItemService loginItemService)
    {
        _coordinator = coordinator;
        _catalog = catalog;
        _profileStore = profileStore;
        _downloadFolderService = downloadFolderService;
        _ytDlpInstaller = ytDlpInstaller;
        _ffmpegInstaller = ffmpegInstaller;
        _denoInstaller = denoInstaller;
        _binaryInstallService = binaryInstallService;
        _fileSystemLauncher = fileSystemLauncher;
        _loginItemService = loginItemService;
        Profiles = new ObservableCollection<DownloadProfile>();
        OptionSections = new ObservableCollection<OptionSectionViewModel>();
    }

    public ObservableCollection<DownloadProfile> Profiles { get; }
    public ObservableCollection<OptionSectionViewModel> OptionSections { get; }
    public IReadOnlyList<JsRuntimeEngineDefinition> JsRuntimeEngineOptions => JsRuntimeEngines.All;
    public IReadOnlyList<ThemePreference> ThemePreferenceOptions { get; } =
        [ThemePreference.System, ThemePreference.Light, ThemePreference.Dark];
    public IReadOnlyList<string> Sections => _catalog.GetSections();

    public AppConfiguration Config
    {
        get => _config;
        private set => SetProperty(ref _config, value);
    }

    public DownloadProfile Profile
    {
        get => _profile;
        private set
        {
            if (SetProperty(ref _profile, value))
            {
                RebuildOptionSections();
                UpdateCliPreview();
            }
        }
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

    public string CliPreview
    {
        get => _cliPreview;
        private set => SetProperty(ref _cliPreview, value);
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set => SetProperty(ref _newProfileName, value);
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
            UpdateCliPreview();
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
            UpdateCliPreview();
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
            UpdateCliPreview();
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
            UpdateCliPreview();
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

    public string ExtraArgs
    {
        get => Profile.ExtraArgs;
        set
        {
            Profile.ExtraArgs = value;
            OnPropertyChanged();
            UpdateCliPreview();
        }
    }

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

        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);

        var active = profiles.FirstOrDefault(p => p.Id == config.ActiveProfileId) ?? profiles[0];
        Profile = CloneProfile(active);
        RebuildOptionSections();
        UpdateCliPreview();
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

    public async Task<bool> SaveAsync()
    {
        var errors = _coordinator.Validate(Config, Profile);
        if (errors.Count > 0)
        {
            ValidationError = string.Join(Environment.NewLine, errors);
            return false;
        }

        if (!_downloadFolderService.IsConfigured(Config))
        {
            ValidationError = "Download folder is required.";
            return false;
        }

        if (_downloadFolderService.TryNormalize(Config.DownloadFolder, out var normalized))
        {
            if (!_downloadFolderService.EnsureExists(normalized))
            {
                ValidationError = "Download folder path is invalid or cannot be created.";
                return false;
            }

            Config.DownloadFolder = normalized;
        }

        ValidationError = null;
        await _coordinator.SaveAsync(Config, Profile);
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

        return true;
    }

    public Task SelectProfileAsync(DownloadProfile profile)
    {
        Profile = CloneProfile(profile);
        Config.ActiveProfileId = profile.Id;
        UpdateCliPreview();
        return Task.CompletedTask;
    }

    public async Task CreateProfileAsync()
    {
        var name = string.IsNullOrWhiteSpace(NewProfileName) ? "New profile" : NewProfileName.Trim();
        var profile = new DownloadProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Options = new Dictionary<string, object?>(Profile.Options, StringComparer.Ordinal),
            ExtraArgs = Profile.ExtraArgs,
        };

        await _profileStore.SaveAsync(profile);
        NewProfileName = string.Empty;
        await LoadAsync();
        await SelectProfileAsync(profile);
    }

    public async Task DuplicateProfileAsync()
    {
        var duplicate = await _coordinator.DuplicateProfileAsync(Profile.Id);
        await LoadAsync();
        await SelectProfileAsync(duplicate);
    }

    public async Task DeleteProfileAsync()
    {
        await _coordinator.DeleteProfileAsync(Profile.Id);
        await LoadAsync();
        if (Profiles.Count > 0)
            await SelectProfileAsync(Profiles[0]);
    }

    public Task<string?> TestYtDlpAsync() => _coordinator.TestYtDlpAsync(Config);

    public Task<string?> TestFfmpegAsync() => _coordinator.TestFfmpegAsync(Config);

    public Task<string?> TestJsRuntimeAsync() => _coordinator.TestJsRuntimeAsync(Config);

    public string ResolvedYtDlpPath => _coordinator.ResolveYtDlpPath(Config) ?? "Not found";

    public string ResolvedFfmpegPath => _coordinator.ResolveFfmpegPath(Config) ?? "Not found";

    public void ApplyTestResult(string toolName, string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            ValidationError = $"{toolName} test failed.";
            StatusMessage = null;
            return;
        }

        if (result.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || result.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            ValidationError = result;
            StatusMessage = null;
            return;
        }

        ValidationError = null;
        StatusMessage = $"{toolName}: {result}";
        OnPropertyChanged(nameof(ResolvedYtDlpPath));
        OnPropertyChanged(nameof(ResolvedFfmpegPath));
        OnPropertyChanged(nameof(ResolvedJsRuntimePath));
        OnPropertyChanged(nameof(JsRuntimesArgumentPreview));
    }

    public void OnOptionChanged() => UpdateCliPreview();

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

    private void RebuildOptionSections()
    {
        OptionSections.Clear();
        foreach (var section in Sections)
        {
            var items = _catalog.GetBySection(section)
                .Select(def =>
                {
                    Profile.Options.TryGetValue(def.Flag, out var value);
                    var item = new OptionItemViewModel(def, value);
                    item.PropertyChanged += (_, _) =>
                    {
                        Profile.Options[def.Flag] = item.Value;
                        OnOptionChanged();
                    };
                    return item;
                })
                .ToList();

            OptionSections.Add(new OptionSectionViewModel(section, items));
        }
    }

    private void UpdateCliPreview() => CliPreview = _coordinator.BuildCliPreview(Config, Profile);

    private static DownloadProfile CloneProfile(DownloadProfile source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        ExtraArgs = source.ExtraArgs,
        SchemaVersion = source.SchemaVersion,
        Options = new Dictionary<string, object?>(source.Options, StringComparer.Ordinal),
    };
}
