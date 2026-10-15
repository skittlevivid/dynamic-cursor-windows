# dynamic cursors for windows

native port of the popular [hypr-dynamic-cursors](https://github.com/VirtCode/hypr-dynamic-cursors) linux (hyprland/wayland) plugin to windows 10/11.

dynamic motion effects, tilt, rotation, stretch, inertia, and shake-to-find magnification for your mouse cursor with zero external dependencies.

> [!note]
> **performance note:** on low-end devices, old cpus, or very weak hardware, the real-time physics simulation and overlay rendering may cause slight lag or stuttering.

---

## features

- **tilt (air drag)**: simulates aerodynamic drag. cursor smoothly tilts based on mouse velocity and horizontal acceleration.
- **rotate (stick simulation)**: simulates a rigid physical rod. cursor realistically trails behind the click hotspot, rotating along movement direction.
- **stretch (squash & stretch)**: cartoon-style stretch dynamics along the velocity vector.
- **shake to find (macos / kde style)**: rapidly shaking the mouse magnifies the cursor (customizable up to 6x) with a configurable return timeout.
- **full custom cursor pack support (.cur, .ani, .png)**:
  - drop any `.cur`, `.ani` (animated cursors with frames and frame rates), or `.png` files into `cursors/`.
  - built-in `filesystemwatcher` reloads cursor assets on the fly with no restart required.
  - customizable base scale from 1 to 100.
  - dynamic scaling without clipping or blurriness.
- **smart windows 10/11 shell integration**:
  - zero idle overhead (0% cpu, persistent 32-bit dib blitting, ~35 mb ram).
  - shell awareness: hides overlay seamlessly over start menu, system tray flyouts, and context menus to prevent double cursor overlap.
  - automatic system cursor hiding and reliable restoration on exit or crash.
- **system tray app & settings gui**:
  - tray icon with live mode toggling.
  - interactive settings window for all physics values, shake parameters, and cursor scaling.
  - global hotkeys for instant control.

---

## hotkeys

| hotkey | action |
|---|---|
| `ctrl + alt + q` | immediately exit and restore default windows cursor |
| `ctrl + alt + d` | toggle dynamic cursor effects on / off |

---

## installation & usage

### option 1: prebuilt binary
1. download the latest release from [releases](../../releases).
2. extract the archive into any folder.
3. launch `run.bat` (or `dynamic-cursors.exe`).
4. look for the cursor icon in the system tray (near the clock).

### option 2: build from source
no heavy sdk or visual studio required. building uses the standard `csc.exe` compiler built into any windows 10/11 system:

```powershell
powershell -executionpolicy bypass -file .\build.ps1
```

or simply run `build.bat`.

---

## folder structure

```text
dynamic-cursors/
├── cursors/          # put your custom .cur, .ani, and .png cursors here
├── src/              # c# source code (.net framework 4.5+)
│   ├── Config.cs
│   ├── CursorHook.cs
│   ├── CursorRenderer.cs
│   ├── OverlayWindow.cs
│   ├── Physics.cs
│   ├── Program.cs
│   ├── SettingsForm.cs
│   └── TrayContext.cs
├── build.bat         # quick build script
├── build.ps1         # powershell build script
├── run.bat           # launch application
├── restore.bat       # emergency restore cursor script
└── README.md
```

---

## custom cursor naming

the following cursor file names are recognized inside `cursors/` (in `.cur`, `.ani`, or `.png` format):

- `arrow` — default pointer arrow
- `link` — link pointer (hand)
- `beam` / `ibeam` — text selection beam
- `cross` — crosshair / precision pointer
- `wait` — busy / wait cursor (sandglass or wheel)
- `sizewe` / `sizens` / `sizeall` / `sizenwse` / `sizenesw` — window resize cursors

---

## emergency cursor restore

if the app was forcefully killed via task manager and your windows cursor remained hidden:
- press `win + r`, enter one of the following commands and press enter:
  ```cmd
  restore.bat
  ```
  or:
  ```cmd
  dynamic-cursors.exe --restore
  ```
- or simply sign out and sign back into windows (`ctrl + alt + del` -> sign out).

---

## license

distributed under the [mit license](license).
