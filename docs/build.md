# Build, test, and publish

## Solution structure

```
TubeGrabber.slnx
├── src/
│   ├── TubeGrabber.Core/           Business logic (queue, yt-dlp invocation, settings)
│   └── TubeGrabber.UI/             Avalonia desktop UI
└── tests/
    ├── TubeGrabber.Core.Tests/
    └── TubeGrabber.UI.Tests/
```

## Prerequisites

- Docker (for the dev container via `make`), or .NET 8 SDK on the host with `IN_CONTAINER=1`
- `yt-dlp` and `ffmpeg` on PATH or installed via the UI

## Commands

```bash
make ci
make coverage
make publish-ui RID=linux-x64
```

Host without container:

```bash
IN_CONTAINER=1 make ci
```

`make coverage` runs both test projects with `coverlet.mtp` (Microsoft.Testing.Platform) and writes one Cobertura file per test project:

- `artifacts/coverage/TubeGrabber.Core.Tests.cobertura.xml`
- `artifacts/coverage/TubeGrabber.UI.Tests.cobertura.xml`

ReportGenerator merges both files into one HTML report at `artifacts/coverage/html/index.html`.

Coverage gates (`--coverlet-threshold`, total; settings live in each test csproj and apply when `-p:Coverage=true`): `TubeGrabber.UI.Tests` uses **line** coverage ≥85%; `TubeGrabber.Core.Tests` uses **method** coverage ≥85% (subprocess/installer types are excluded by file and covered via integration-style tests). Line coverage for Core is reported in the HTML report.

## Target framework for Windows builds

`src/TubeGrabber.UI/TubeGrabber.UI.csproj` targets `net10.0`.
When the runtime identifier starts with `win` (for example `make publish-ui-win` or `RID=win-x64`), the project targets `net10.0-windows10.0.17763.0` instead.
Toast notifications need this target, because they use the Windows-only `Microsoft.Toolkit.Uwp.Notifications` package.
Other platforms build against plain `net10.0`.
