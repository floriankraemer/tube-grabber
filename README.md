# Tube Grabber

Desktop UI for [yt-dlp](https://github.com/yt-dlp/yt-dlp): a download queue, profile-based settings, and optional yt-dlp/ffmpeg setup—without living on the command line.

![Tube Grabber main window](docs/screenshot.png)

## Features

- **Download queue** — paste URLs, track status and progress, cancel or remove jobs
- **URL cleanup** — strips common tracking query parameters from pasted YouTube links
- **Download folder** — choose on first launch; change anytime in settings
- **Profiles** — named yt-dlp option sets with a built-in option catalog, CLI preview, and quote-aware extra arguments
- **Built-in profiles** — Default (single MP4), Download Audio as mp3, Download HQ Video (see below)
- **Binaries** — set or browse yt-dlp/ffmpeg paths, test each, or install from the Settings page
- **Settings** — tabbed editors for common yt-dlp flags (subs, metadata, SponsorBlock, and more)
- **Job logs** — view yt-dlp output for failed (or finished) jobs
- **YouTube search** — find videos (filter by upload date and length, Shorts hidden by default) or channels, then queue a download or subscribe straight from the results
- **Channel subscriptions** — follow YouTube channels and queue their new uploads automatically
- **Background mode and notifications** — keep checking while the window is closed, and get an OS notification when a download finishes or fails

## Built-in profiles

Created under `~/.tube-grabber/profiles/` on first run (missing profiles are added without overwriting existing ones):

| Profile | Purpose |
|---------|---------|
| **Default** | Single MP4 when available, otherwise best merged video+audio |
| **Download Audio as mp3** | Best audio → MP3, SponsorBlock, parallel fragments |
| **Download HQ Video** | Up to 1080p H.264/AAC as one `.mp4` file |

Pick the active profile in **Settings → Profiles**. Details: [docs/settings.md](docs/settings.md).

## Getting started (users)

1. Build or download a self-contained app (see [Build](#build-and-test) below).
2. On first launch, choose a download folder.
3. Install or point to **yt-dlp** and **ffmpeg** (**Settings → Binaries** in the sidebar).
4. Paste a URL, add to the queue, and start downloads.

Config and profiles live in `~/.tube-grabber/` (`app.json`, `profiles/*.json`).
Data from the former `~/.yt-dlp-ui/` folder is copied there on first start (see [docs/settings.md](docs/settings.md)).

## Build and test

Requires **Docker** for default `make` targets (dev container), **or** .NET 8 SDK on the host with `IN_CONTAINER=1`.

```bash
make ci              # restore, Release build, tests
make coverage        # tests + HTML report in artifacts/coverage/html/
make publish-ui-win  # Windows x64 → artifacts/publish-ui-win-x64/TubeGrabber.exe
make publish-ui      # host RID → artifacts/publish-ui-<RID>/TubeGrabber
```

`make build-release` compiles Release assemblies only; use **`make publish-ui`** (or `publish-ui-win`) for a runnable app.

Host SDK without container:

```bash
IN_CONTAINER=1 make ci
IN_CONTAINER=1 make publish-ui RID=linux-x64
```

More: [docs/build.md](docs/build.md).

## Project structure

```
TubeGrabber.slnx
├── src/
│   ├── TubeGrabber.Core/       Queue, yt-dlp invocation, profiles, config, installers
│   └── TubeGrabber.UI/         Avalonia desktop UI
└── tests/
    ├── TubeGrabber.Core.Tests/
    └── TubeGrabber.UI.Tests/
```

## Documentation

- [docs/build.md](docs/build.md) — solution layout, coverage gates, publish options
- [docs/settings.md](docs/settings.md) — config paths, binaries, download folder, profiles, subscriptions, background mode, security

## Legal

You must comply with the terms of service of content providers and applicable law. This project is an unofficial front-end for yt-dlp; the authors are not affiliated with YouTube or Google.

## License

See [LICENSE](LICENSE).
