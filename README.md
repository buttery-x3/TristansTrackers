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

Packaged, self-contained Windows builds are also available from the GitHub
Releases page.

## Usage

- Drag the bar with the left mouse button to move it.
- Use the lock button to toggle whether the bar can be moved.
- Hover over the bar and select the alarm-clock button to start a 1- or
  2-minute alarm, an alarm from 5 to 60 minutes in 5-minute increments, or a
  90-minute or 2-hour alarm.
- While an alarm is active, its bar fills from left to right. Hover to see its
  remaining whole minutes, or open the alarm menu again to replace or cancel
  it.
- When an alarm expires, its bar is replaced by a large alarm-clock icon above
  the tracker spanning the timer bar's width. Click either alarm-clock icon to
  dismiss it.
- Right-click the tracker to open its square dark HUD command menu. The alarm
  picker uses the same menu system and stays above the tracker bars.
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
