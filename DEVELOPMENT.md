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
   the tracker obscured.

During rendering, the app advances an in-memory progress value and updates the
fill bar width against the current border width. The current fill cycle duration
is one second, with a short visual pulse when each cycle completes.

An alarm can be selected in 5-minute increments through 60 minutes, followed
by 90-minute and 2-hour options. Starting an alarm adds a second draining bar
above the tracker without changing the tracker's saved position. The countdown
uses an absolute UTC end time, so it remains accurate across system sleep while
the app is running. Hovering shows whole minutes remaining, rounded up. At
expiry the alarm plays the Windows system alert, pulses, and remains visible
until dismissed. Alarm state is intentionally not persisted across app exits.

When the user drags the unlocked window, the app saves the new `Left` and `Top`
values after the drag finishes. When the alarm row is present, the saved
position still represents the lower tracker bar.

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

## Future Development Notes

Useful next areas for improvement:

- Persist lock state if users expect the HUD to stay locked after restart.
- Make duration, colors, and size configurable through the existing JSON config.
- Add lightweight tests or manual smoke-test notes once behavior grows beyond
  the current single-window app.
