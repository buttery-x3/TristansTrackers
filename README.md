# TristansTrackers

TristansTrackers is a minimal Windows WPF timer/progress HUD. It runs as a
small, borderless, always-on-top bar that can be dragged around the desktop and
locked in place.

The app is intentionally lightweight: it stays out of the taskbar and Alt+Tab
list, animates a simple fill bar with a completion pulse, and saves its last
position between launches.

## Requirements

- Windows
- .NET 8 SDK, or Visual Studio 2022 with the WPF workload installed

## Quick Start

From the repository root:

```powershell
dotnet restore
dotnet build TristansTrackers.sln
dotnet run --project TristansTrackers.csproj
```

## Usage

- Drag the bar with the left mouse button to move it.
- Use the lock button to toggle whether the bar can be moved.
- Hover over the bar and select the alarm-clock button to start an alarm from
  5 to 60 minutes in 5-minute increments, or for 90 minutes or 2 hours.
- While an alarm is active, hover to see its remaining whole minutes. Open the
  alarm menu again to replace or cancel it.
- When an alarm expires, click the alarm-clock button to dismiss it.
- The window is always on top of normal windows.
- The window is hidden from the taskbar and Alt+Tab list.

## Configuration

TristansTrackers stores its window position and size in:

```text
%APPDATA%\TristansTrackers\timebar_config.json
```

The file is created automatically on first launch. If the saved position is not
set yet, the app centers the bar on the primary monitor work area and saves that
initial position.

## Development Notes

See [DEVELOPMENT.md](DEVELOPMENT.md) for architecture notes, runtime flow, the
configuration schema, and the current build baseline.

## License

This project is licensed under the GNU Affero General Public License v3.0. See
[LICENSE.txt](LICENSE.txt).
