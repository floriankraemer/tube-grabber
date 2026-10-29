# Settings and profiles

Configuration is stored under `~/.yt-dlp-ui/`:

- `app.json` — yt-dlp/ffmpeg paths, max concurrent downloads, theme, active profile, background settings
- `profiles/*.json` — named download profiles with common yt-dlp options
- `subscriptions.json` — channel subscriptions and their state (see [Subscriptions and background mode](#subscriptions-and-background-mode))
- `instance.lock` — lock file that keeps a second app instance from starting

## Appearance

Open **Settings → Appearance** to choose **System**, **Light**, or **Dark**. **System** (default) follows your operating system theme on Windows, Linux, and macOS. **Light** and **Dark** override the OS setting. The choice is stored in `app.json` as `ThemePreference`.

## Binaries

Open **Settings → Binaries** in the sidebar to set paths to **yt-dlp**, **ffmpeg**, and a **JavaScript runtime** for YouTube, browse for executables, and run **Test** on each. Leave a path blank to use a bundled install or tools on your `PATH`. The resolved path shows what the app will actually run.

| Button | Action |
|--------|--------|
| **Install** | Downloads the latest release for your operating system and CPU architecture into `~/.yt-dlp-ui/bin/` |
| **Releases** | Opens the official GitHub releases page in your browser |
| **Browse** | Select an executable you downloaded or installed yourself |
| **Test** | Verifies the resolved binary runs |

If **Install** fails, the app shows an error with the release page URL and opens that page so you can download the correct file manually, then use **Browse** to point the app at it.

Release pages:

- [yt-dlp](https://github.com/yt-dlp/yt-dlp/releases)
- [ffmpeg (yt-dlp FFmpeg-Builds)](https://github.com/yt-dlp/FFmpeg-Builds/releases)
- [Deno](https://github.com/denoland/deno/releases) (when Deno is selected as the JS runtime)

### JavaScript runtime (YouTube)

Since [yt-dlp 2025.11.12](https://github.com/yt-dlp/yt-dlp/issues/15012), a JS runtime (Deno recommended) is required for full YouTube support. Choose an engine in the dropdown and optionally set its executable path. The app passes `--js-runtimes ENGINE` or `--js-runtimes ENGINE:PATH` to yt-dlp. See the [EJS wiki](https://github.com/yt-dlp/yt-dlp/wiki/EJS) for install details.

| Engine | Notes |
|--------|--------|
| **Default** | Lets yt-dlp use its built-in lookup (Deno on PATH only) |
| **Deno** | Recommended |
| **Node** | Minimum v20 |
| **QuickJS / QuickJS-ng / Bun** | Optional alternatives |

## Download folder

On first launch you are asked to choose a download folder (default suggestion: `~/youtube-downloads`). Change it anytime under **Settings → Queue**. Profile option `-P` overrides this folder when set.

## Profiles

Built-in profiles (created on first run if missing):

| Profile | Purpose |
|---------|---------|
| **Default** | Single MP4 when available (`b[ext=mp4]/bv*+ba/b`), otherwise best merged video+audio |
| **Download Audio as mp3** | Like `downloadaudio2.ps1`: best audio → MP3, `%(channel)s/%(title)s`, SponsorBlock, `--force-overwrites` (avoids stale resume / HTTP 416) |
| **Download HQ Video** | Up to 1080p H.264/AAC, one `.mp4` file (progressive or merged) |

Each profile stores option values from the built-in catalog plus optional **Extra arguments** (quote-aware). Use Extra arguments for flags not exposed in the UI.

## Subscriptions and background mode

A subscription follows one YouTube channel.
The app checks the channel once at startup and then at a fixed interval.
Each new upload goes into the queue with the profile you chose for that subscription.

### Adding a subscription

Open **Subscriptions** in the main window, or choose **Subscriptions…** in the tray menu. Paste a channel URL in one of these forms:

- `https://www.youtube.com/@handle`, or a bare `@handle`
- `https://www.youtube.com/channel/UC…`
- `https://www.youtube.com/c/name`
- `https://www.youtube.com/user/name`

Set the profile and the number of existing videos to download, from 0 to 30, then select **Subscribe**.
The app downloads the latest N videos right away and leaves older videos alone.
After that, only new uploads are downloaded.

Each subscription has an enable toggle.
A disabled subscription keeps its state, but the app does not check it.

### What the app checks

Each check reads the 30 most recent videos on the channel's Videos tab.
Shorts are not on that tab, so they are never queued.
The app also skips live streams, upcoming premieres, and members-only videos.
If more than 30 uploads appear between two checks, the older ones are missed.

A video counts as handled when its download completes, fails, is cancelled, or is removed from the queue.
The app does not retry a failed video.
A download that was still running when you quit the app is not marked as handled, so the next check queues it again.

### Check interval

Open **Settings → Background** to set **Check subscriptions every** to a value from 15 to 1440 minutes.
The default is 60 minutes.
To check now, select **Check now** in the Subscriptions window, or choose **Check subscriptions now** in the tray menu.

### Background mode

With **Keep running in the system tray when the window is closed** enabled (default), closing the main window hides it, and the app keeps running in the tray.
The first time the window is hidden in a session, the app shows a notification that it is still running.
Use **Quit** in the tray menu to exit.

The tray menu has these items:

| Item | Action |
|------|--------|
| **Open Yt-dlp-ui** | Shows the main window |
| **Subscriptions…** | Opens the Subscriptions window |
| **Check subscriptions now** | Runs a check outside the normal interval |
| **Quit** | Stops the subscription checks and exits the app |

On Linux, GNOME does not show tray icons by default.
Install an AppIndicator or StatusNotifier extension to see the icon.
Without the extension, the app still runs.
Start the app again to bring its window to the front.

### Notifications

The app sends a desktop notification when a subscription download completes (**New video downloaded**) or fails (**Download failed**).

| Operating system | Mechanism |
|------------------|-----------|
| Windows | Toast notification |
| Linux | Freedesktop notification over D-Bus (`org.freedesktop.Notifications`) |
| macOS | Notification Center |

### Start at login

Enable **Start at login (minimized to tray)** in **Settings → Background** to start the app when you sign in.
The app writes its login entry when you save the setting.

| Operating system | Location |
|------------------|----------|
| Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Yt-dlp-ui` |
| Linux | `~/.config/autostart/yt-dlp-ui.desktop` (uses `XDG_CONFIG_HOME` when set) |
| macOS | `~/Library/LaunchAgents/com.github.floriankraemer.ytdlpui.plist` |

The login entry starts the app with `--minimized`.
With the tray available, the app starts without a window.
Without a tray, the window stays visible, because a hidden app would have no way to show itself again.

### Single instance

The app runs one instance at a time.
When you start it again, the running window comes to the front, and the new process exits.

### Settings in `app.json`

| Key | Default | Meaning |
|-----|---------|---------|
| `SubscriptionCheckIntervalMinutes` | `60` | Minutes between subscription checks. Values outside 15 to 1440 are clamped. |
| `RunInBackground` | `true` | Keep the app in the tray when the main window closes |
| `StartAtLogin` | `false` | Register the app as a login item for your operating system |

### Subscription state

`subscriptions.json` holds each subscription's channel ID, name, profile, enable state, last check time, and last error.
It also stores the IDs of up to 500 recently handled videos per channel, so the app does not queue them again.
On Linux and macOS, the app restricts the file permissions, as it does for other config files.

## Security

Profiles may contain cookies or passwords. Files are stored only on your machine. On Linux/macOS the app sets restrictive permissions where supported.

## Legal

You are responsible for complying with the terms of service of content sites and applicable laws. This app is a front-end for [yt-dlp](https://github.com/yt-dlp/yt-dlp).
