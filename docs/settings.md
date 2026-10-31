# Settings and profiles

Configuration is stored under `~/.tube-grabber/`:

- `app.json` — yt-dlp/ffmpeg paths, max concurrent downloads, theme, active profile, background settings
- `profiles/*.json` — named download profiles with common yt-dlp options
- `subscriptions.json` — channel subscriptions and their state (see [Subscriptions and background mode](#subscriptions-and-background-mode))
- `logs/app-YYYY-MM-DD.log` — diagnostic log, see [Log files](#log-files)
- `instance.lock` — lock file that keeps a second app instance from starting

### Migration from Yt-dlp-ui

The app was called Yt-dlp-ui and stored its data in `~/.yt-dlp-ui/`.
On startup, if `~/.tube-grabber/` does not exist and `~/.yt-dlp-ui/` does, the app copies the old folder to the new one.
The copy goes into a temporary sibling folder first and is renamed when complete, so an interrupted copy never leaves a half-migrated folder.
If the copy fails, the app starts with a fresh configuration and writes the reason to the log.
The old folder is left untouched, so an older version can still use it.
Stored absolute paths that point into the old folder, such as the installed yt-dlp binary, are rewritten to the new folder.
If start at login was enabled under the old name, the app removes the old login entry and registers the new one on the next start.

## Saving settings

Settings is a page in the main window, reached with **Settings** at the bottom of the sidebar.
Changes stay in the page until you select **Save changes**.
Select **Discard** to reload the saved settings and drop your edits.
The footer shows **Unsaved changes in N profiles** (or **Unsaved changes**) while something differs from what is saved.
If a profile has a validation error, the footer shows the error with the profile name.
Select the error to open that profile.
When you leave Settings with unsaved changes, the app asks whether to **Save**, **Discard**, or **Cancel** and stay on Settings.
Closing the window does not ask.

## Log files

The app writes errors and diagnostics from background work (subscription checks, notifications, tray, startup) and unhandled exceptions to `~/.tube-grabber/logs/app-YYYY-MM-DD.log`.
Each line has a timestamp, level, source and message; errors include the exception with its stack trace.
There is one file per day, and only the newest 7 files are kept.
On Linux and macOS the folder and files are readable by your user only.
If the log cannot be written, for example on a read-only disk, logging is skipped silently.

## Appearance

Open **Settings → Appearance** to choose **System**, **Light**, or **Dark**. **System** (default) follows your operating system theme on Windows, Linux, and macOS. **Light** and **Dark** override the OS setting. The choice is stored in `app.json` as `ThemePreference`.

## Binaries

Open **Settings → Binaries** in the sidebar to set paths to **yt-dlp**, **ffmpeg**, and a **JavaScript runtime** for YouTube, browse for executables, and run **Test** on each. Leave a path blank to use a bundled install or tools on your `PATH`. The resolved path shows what the app will actually run.

| Button | Action |
|--------|--------|
| **Install** | Downloads the latest release for your operating system and CPU architecture into `~/.tube-grabber/bin/` |
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

Open **Settings → Profiles** to see all profiles.
A profile is a reusable set of yt-dlp options.
You pick one when you download, search or subscribe.
Opening a profile in Settings does not change the profile that is pre-selected on those pages.

Built-in profiles (created on first run if missing):

| Profile | Purpose |
|---------|---------|
| **Default** | Single MP4 when available (`b[ext=mp4]/bv*+ba/b`), otherwise best merged video+audio |
| **Download Audio as mp3** | Like `downloadaudio2.ps1`: best audio → MP3, `%(channel)s/%(title)s`, SponsorBlock, `--force-overwrites` (avoids stale resume / HTTP 416) |
| **Download HQ Video** | Up to 1080p H.264/AAC, one `.mp4` file (progressive or merged) |

Each profile stores option values from the built-in catalog plus optional **Extra arguments** (quote-aware).
Use Extra arguments for flags not exposed in the UI.

### Profile list

Each profile is a card with an icon, its name, a one-line summary, and how many subscriptions use it.
The summary reads like `Audio only → MP3 · SponsorBlock · 9 options set`.
A profile that sets no options reads `yt-dlp defaults`.
The **Built-in** pill marks the three built-in profiles.
The **Unsaved** pill marks a profile whose edits are not saved yet.
Select a card, or press Enter on it, to open the profile.
The **⋯** button on a card opens a menu with these actions:

| Action | Effect |
|--------|--------|
| **Edit** | Opens the profile |
| **Duplicate** | Copies the profile, including unsaved edits, as `Name (copy)`, `Name (copy 2)` and so on, saves it, and opens it |
| **Reset to built-in settings** | Built-in profiles only. Puts the options and extra arguments back to what the app ships. The name stays. The profile becomes unsaved until you save |
| **Delete…** | Custom profiles only. Asks for confirmation first |

**New profile** asks for a name and a starting point.
The name must be unique, ignoring case.
**Start from** copies any profile, or starts empty with **yt-dlp defaults**.
The new profile is saved at once and opens.

### Profile page

The page header shows a breadcrumb.
Select **Profiles**, press Alt+Left, or press Esc outside a text box to go back to the list.
Your edits are kept while you go back, and the list shows them as **Unsaved**.

The header card shows the summary, the number of options set, and the subscriptions that use the profile.
**Rename** changes the name of a custom profile.
The new name is saved with **Save changes**.
F2 also opens Rename.
Built-in names are locked, because the app finds its own profiles by them.
**Duplicate** and the **⋯** menu work as in the list.

Options are grouped in sections.
Sections with changed options come first and start open.
Each row shows a short label, the yt-dlp flag, and a description.
A row that differs from the default has an accent bar and a **Reset to default** button.
The button writes the default value explicitly, so a built-in profile does not get its old value back on the next start.
**Command line** at the end holds the **Extra arguments** and a read-only preview of the yt-dlp command.
**Copy** puts the preview on the clipboard.

Use the search box (Ctrl+F) to find an option by label, flag, description, or section name.
It also searches the extra arguments.
Sections without a match are hidden, and sections with a match open.
Esc clears the search.
**Changed only** hides all options that still have their default value.
It combines with the search.

### Deleting a profile

Only custom profiles can be deleted.
Built-in profiles come back on the next start, so the app does not offer to delete them.
The confirmation names two consequences when they apply:

- Subscriptions that use the profile switch to **Default**.
- Unsaved changes of the profile are lost.

Deleting removes the profile file at once.
You cannot delete the last profile.

## Subscriptions and background mode

A subscription follows one YouTube channel.
The app checks the channel once at startup and then at a fixed interval.
Each new upload goes into the queue with the profile you chose for that subscription.

### Adding a subscription

Select **Subscriptions** in the sidebar of the main window, or choose **Subscriptions…** in the tray menu, which shows the main window on the Subscriptions page. Paste a channel URL in one of these forms:

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

A video counts as handled when its download completes, is cancelled, or is removed from the queue.
A failed download does not count as handled.
The next check queues the video again.
After 3 failed attempts, the app gives up on that video and marks it as handled.
A download that was still running when you quit the app is not marked as handled, so the next check queues it again.

### Check interval

Open **Settings → Background** to set **Check subscriptions every** to a value from 15 to 1440 minutes.
The default is 60 minutes.
To check now, select **Check now** on the Subscriptions page, or choose **Check subscriptions now** in the tray menu.

### Background mode

With **Keep running in the system tray when the window is closed** enabled (default), closing the main window hides it, and the app keeps running in the tray.
The first time the window is hidden in a session, the app shows a notification that it is still running.
Use **Quit** in the tray menu to exit.

The tray menu has these items:

| Item | Action |
|------|--------|
| **Open Tube Grabber** | Shows the main window |
| **Subscriptions…** | Shows the main window on the Subscriptions page |
| **Check subscriptions now** | Runs a check outside the normal interval |
| **Quit** | Stops the subscription checks and exits the app |

On Linux, GNOME does not show tray icons by default.
Install an AppIndicator or StatusNotifier extension to see the icon.
Without the extension, the app still runs.
Start the app again to bring its window to the front.

### Notifications

The app sends a desktop notification when a subscription download completes (**New video downloaded**).
It also sends one when it gives up on a video after 3 failed attempts (**Download failed**).
It sends no notification for a failed attempt that it will retry.

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
| Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Tube Grabber` |
| Linux | `~/.config/autostart/tube-grabber.desktop` (uses `XDG_CONFIG_HOME` when set) |
| macOS | `~/Library/LaunchAgents/com.github.floriankraemer.tubegrabber.plist` |

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
For videos that failed but are not yet handled, it stores the number of failed attempts.
On Linux and macOS, the app restricts the file permissions, as it does for other config files.

## Security

Profiles may contain cookies or passwords. Files are stored only on your machine. On Linux/macOS the app sets restrictive permissions where supported.

## Legal

You are responsible for complying with the terms of service of content sites and applicable laws. This app is a front-end for [yt-dlp](https://github.com/yt-dlp/yt-dlp).
