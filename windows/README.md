# ClipBoard for Windows

A native Windows port of [HMZ-E/ClipBoard](https://github.com/HMZ-E/ClipBoard), originally built with SwiftUI and AppKit for macOS. This port uses C#, WPF, and Windows clipboard notifications. The original logo and the history/pinned-card design are retained.

## Run

From the repository root, double-click **`Run.cmd`**. It builds `build\ClipBoard.exe` on first launch and starts it. You can then run that executable directly. The app opens beside the taskbar and keeps running in the notification area when you close its panel. Click the clipboard tray icon or press **Ctrl+Shift+V** to reopen it. If the icon is hidden, look under the tray's **^** menu.

For a portable copy, unzip **`dist\ClipBoard-Windows-portable.zip`** and run `ClipBoard.exe`. Keep the executable in a permanent folder before enabling **Start with Windows** in Settings. To exit completely, right-click its tray icon and choose **Quit ClipBoard**.

Requires Windows 10 or 11 with **.NET Framework 4.8** enabled. This is the Windows Framework, separate from the .NET 8 runtime. No third-party packages, SDK installation, administrator privileges, or network access are needed to run it. The executable is unsigned.

## Features

- Text and bitmap-image capture using clipboard change notifications, with retries when another application holds the clipboard.
- Up to 50 unpinned entries, with a separate persistent pinned shelf.
- Search, timestamps, click-to-copy, image previews, full text previews, and Expand/Collapse.
- Link domains, monospace code, and hex-color swatches. Captured text retains its original whitespace and Unicode.
- Delete with six seconds to Undo, including Ctrl+Z.
- Chrome, Sunset, Midnight, and Custom themes; three native color pickers.
- Animated panel opening/closing, staggered card entrances, hover lifts, copy pulses, preview zoom/fades, and smooth mouse-wheel scrolling with card depth.
- Continuously moving background glows and gradual color transitions between themes. Background animation stops while the panel is hidden.
- Full, Reduced, and Off animation modes in Settings; Full is enabled by default. Reduced keeps fades and disables continuous motion and zoom. Soft, Balanced, and Bold control scrolling depth.
- Pause/resume capture, optional startup at sign-in, and one running instance per Windows user/session.
- Encrypted local persistence using Windows DPAPI, atomic saves, and backup recovery.

## Keyboard

| Shortcut | Action |
| --- | --- |
| Ctrl+Shift+V | Open/hide the panel globally |
| Up / Down | Navigate pinned items and history |
| Down in Search | Move from Search into the cards |
| Enter | Copy selected item |
| Space | Preview selected item |
| Delete | Delete selected item |
| Ctrl+Z | Undo deletion while available; Search keeps normal text undo |
| Esc | Close preview/settings, clear a search, then hide the panel |

After copying an item, switch to the destination app and paste with **Ctrl+V**. Ctrl+C retains the normal Windows copy behavior. If another program owns Ctrl+Shift+V, ClipBoard shows a message and remains accessible from its tray icon.

## Local data

Saved data lives in `%LOCALAPPDATA%\HMZE\ClipBoard\history.dat`. History, pins, and settings are encrypted for the current Windows account. No cloud sync, telemetry, or network code is included. Clearing history also removes its previous backup.

The clipboard present before launch is skipped. Copies made while paused are also skipped when capture resumes. The app honors `ExcludeClipboardContentFromMonitorProcessing` and `CanIncludeInClipboardHistory=0` when supplied by another application. Other clipboard content, including passwords without those markers, can be saved; pause capture when needed.

To bound storage, text is limited to 1,048,576 UTF-16 characters, PNG data to 8 MiB per image, decoded input images to 25 million pixels, and total retained payload to 64 MiB. Old unpinned entries are removed to meet the limit. Pins are preserved; if pins fill the limit, new copies are rejected with a visible message.

## Build and verify

Run these commands from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

`build.ps1` uses the C# compiler that ships with the Windows .NET Framework. It embeds the logo, executable icon, and DPI-aware application manifest. `windows\ClipBoard.csproj` is available for Visual Studio with .NET Framework 4.8 targeting tools.

The self-tests use a fake clipboard adapter, isolated encrypted storage, native listener/hotkey registration, and offscreen WPF renders. Animation checks advance the dispatcher over time and compare intermediate property values and rendered frames, including opening/closing, delayed card entrances, hover/copy feedback, scrolling, background movement, theme transitions, and Reduced/Off behavior. They do not read or overwrite the system clipboard or modify sign-in startup. Results and screenshots are written to `artifacts`. Live capture/paste across different applications still needs a manual check on the target machine.

## Port notes

The Windows implementation is under `windows`; the original SwiftUI implementation is under `ClipBoard`. The Windows UI uses WPF gradients and scrolling effects instead of SwiftUI's mesh/material rendering. It captures text and bitmap images; file-copy history, rich-text preservation, automatic pasting, and importing macOS history are not implemented.
