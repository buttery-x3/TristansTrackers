# Development Notes

This document captures the current shape of TristansTrackers for future
development. It is intentionally small and should stay aligned with the app as
it exists, not with features that have not been built yet.

## Architecture Overview

TristansTrackers is a single WPF desktop project targeting `net8.0-windows`.
The project uses nullable reference types, implicit usings, and the built-in
`System.Text.Json` serializer for configuration persistence.

The app starts from `App.xaml`, which opens `MainWindow.xaml`.

## Main Components

- `MainWindow.xaml` defines the floating HUD window, the tracker and alarm
  bars, and their hover controls.
- `MainWindow.xaml.cs` owns the window behavior: native window styles,
  animation and alarm timing, dragging, lock state, and config load/save.
- `HudMenuWindow.xaml` and its code-behind implement the reusable square,
  opaque command surface used by every application menu. Each menu is an
  independent native tool window with the same topmost treatment as the
  tracker.
- `HudMenuManager.cs` owns menu creation, placement, lifecycle, and z-order.
  Callers provide only `HudMenuItem` definitions and either an element or
  cursor anchor.
- `TimerBarConfig.cs` defines the JSON-backed window configuration model.

## Runtime Flow

On window load, the app:

1. Gets the native window handle.
2. Applies a Win32 tool-window style so the window stays hidden from Alt+Tab.
3. Hooks `CompositionTarget.Rendering` for frame-synced animation.
4. Loads the saved config from the user's AppData folder.
5. Centers the window on the primary monitor work area on first launch.
6. Applies the configured width, height, and position.
7. Starts a low-frequency z-order check so other topmost windows cannot leave
   the tracker obscured. When a HUD menu is open, the tracker is raised first
   and the menu second so the menu always remains above the bars.

During rendering, the app advances an in-memory progress value and updates the
fill bar width against the current border width. The current fill cycle duration
is one second, with a short visual pulse when each cycle completes.

An alarm can be selected for 1, 2, 5, 10, 15, 20, 30, 45, 60, or 90 minutes,
or for 2 hours. Starting an alarm adds a second bar that
fills from left to right above the tracker without changing the tracker's saved
position. The countdown uses an absolute UTC end time, so it remains accurate
across system sleep while the app is running. Hovering shows whole minutes
remaining, rounded up. At expiry the alarm bar is replaced by an alarm-clock
icon sized to the timer bar's width, the Windows system alert plays, and the
icon pulses and remains visible until dismissed.
Alarm state is intentionally not persisted across app exits.

When the user drags the unlocked window, the app saves the new `Left` and `Top`
values after the drag finishes. When the alarm row is present, the saved
position still represents the lower tracker bar.

## Menu Convention

All current and future menus must use `HudMenuManager`, `HudMenuWindow`, and
`HudMenuItem`. Do not introduce WPF `ContextMenu`, `Popup`, or feature-specific
menu windows. A feature creates a list of item definitions and supplies either
`HudMenuAnchor.ForElement(...)` for a control-anchored menu or
`HudMenuAnchor.AtCursor(...)` for a pointer-anchored menu. This keeps styling,
keyboard behavior, screen-edge placement, cleanup, and coordinated topmost
ordering consistent across the app.

## Configuration

The config file is stored at:

```text
%APPDATA%\TristansTrackers\timebar_config.json
```

It is backed by `TimeBarConfig` and serialized with `System.Text.Json`.

Current schema:

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `XPos` | `double` | `-1` | Saved window left position. |
| `YPos` | `double` | `-1` | Saved window top position. |
| `Width` | `double` | `100` | Window width. |
| `Height` | `double` | `20` | Window height. |

`XPos` or `YPos` values below zero are treated as first-launch/uninitialized
positions and cause the app to center the window before saving config.

## Build Baseline

Agents must always build the solution in the `Release` configuration after
making code changes. This ensures the user has an up-to-date executable for
manual testing:

```powershell
dotnet restore
dotnet build TristansTrackers.sln --configuration Release
```

If a running TristansTrackers instance locks the Release executable, close it
and retry. Do not treat a build that could not update the executable as
successful.

The build should complete without warnings. Before handing work back, report
the build result and the full path to the generated executable:

```text
bin\Release\net8.0-windows\TristansTrackers.exe
```

## Publishing a GitHub Release

The reusable release script builds a self-contained, single-file Windows x64
package, includes the README and license, creates a ZIP and SHA-256 checksum,
and publishes both assets with app description and usage notes:

```powershell
.\scripts\Publish-GitHubRelease.ps1 -Version v0.2.1
```

The script requires an authenticated GitHub CLI session, committed tracked
files, and local `main` aligned exactly with `origin/main`. Generated packages
and release notes are written under the ignored `artifacts` directory.

## Future Development Notes

Useful next areas for improvement:

- Persist lock state if users expect the HUD to stay locked after restart.
- Make duration, colors, and size configurable through the existing JSON config.
- Add lightweight tests or manual smoke-test notes once behavior grows beyond
  the current single-window app.
