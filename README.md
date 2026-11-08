# Clean TV Remote

A compact Windows remote for Samsung Tizen TVs, including the Samsung TU690T (`UN58TU690TFXZA`). Built with C# and Windows Forms on .NET 10. It runs on your home network without a Samsung account, cloud backend, telemetry, or third-party UI packages.

## Redesigned remote

The main window uses a dark, narrow layout inspired by the provided remote screenshot:

- Dark charcoal background, soft circular buttons, purple OK button, and red power button
- TV name and connection indicator across the top
- Power, Input, configurable favorite app launch shortcuts, Netflix, and Type-to-TV text entry
- Large circular four-way navigation pad with OK in the center
- Back, Home, and Options shortcuts
- Volume up/down arrow buttons, optional mouse-wheel volume control, and channel rocker, with mute and info controls
- Minimize-to-tray support and an always-on-top option
- Rewind, Play, and Fast-forward controls
- Hover and press feedback, responsive layout, and Windows keyboard navigation

**Apps** opens a shortcuts menu containing Samsung Home, Netflix, and per-TV custom favorite app shortcuts. **Netflix** requests a launch of Samsung Tizen app `3201907018807` using the local REST application endpoint, with WebSocket fallback. The app must be installed on the TV, and some firmware ignores remote app launches. A request being accepted does not confirm Netflix opened. The **Type** button opens a text entry dialog for compatible Samsung TV text fields. **Options** includes Keyboard shortcuts, Samsung Menu, Pause, Wake TV, reconnect, and pairing management.

## Build a Windows executable

1. Install the [.NET 10 SDK for Windows](https://dotnet.microsoft.com/download/dotnet/10.0) if you do not already have it.
2. Extract this project or clone the GitHub repository.
3. Double-click `build-windows.bat`.
4. Run `publish\CleanTVRemote.exe`.

The build script publishes a self-contained single-file Windows x64 executable. The resulting EXE can be copied to a compatible Windows x64 computer without installing the runtime. The executable is not code-signed and may show an unknown-publisher warning. The app can also be built from `CleanTVRemote.csproj` in Visual Studio.

## Connect to the Samsung TV

1. Turn on your TV and connect it and the PC to the same home network.
2. If available, enable **IP Remote** in the TV's network expert settings.
3. Find the TV's IPv4 address in its network settings or your router's connected-device list.
4. In the app, click **Living Room TV** at the top.
5. Enter the address and select **Secure (port 8002)**.
6. Select **Save & connect**, then approve the connection prompt on the television.

The app keeps the pairing token for future connections. Select **Options > Reconnect** on subsequent launches. If port 8002 fails, you can test Legacy (8001) on a trusted network.

If you want to wake your TV from standby, also provide its **active network interface's MAC address** in the connection settings: use the TV's wired MAC if the TV uses Ethernet, or wireless MAC if it uses Wi-Fi. If available, enable **Settings > General > Network > Expert Settings > Power On with Mobile**. Power-on via Wake-on-LAN may not work over Wi-Fi or when the TV enters deep standby; it is not a guaranteed power-on method. The TV may take time to start accepting WebSocket connections after waking.

When the remote is connected, Power sends `KEY_POWER` instead of `KEY_POWEROFF`. Samsung Tizen models vary in their support for explicit power-off codes. `KEY_POWER` is a **toggle**, not a guaranteed off command; avoid repeated clicks when the TV takes time to respond. The app does not have reliable power-state feedback and intentionally reports only that a command or packet was sent.

## Type text on the TV

1. Connect Clean TV Remote to the Samsung TV.
2. On the TV, open and focus a native text field (for example the built-in browser address bar).
3. Click **Type**, enter a search phrase, and click **Send text**.
4. If the text appears on the TV, use the remote's **OK** button to submit it if needed.

The app sends UTF-8 text using Samsung's `SendInputString` WebSocket command. The command being sent does **not** confirm that the TV accepted the text. Netflix, YouTube, and some other streaming apps use their own keyboard screens and may ignore remote text input. The feature does not inject physical keyboard keys into those apps, and it does not automatically press OK. The remote does not retain or log the text you enter.

## Connection and diagnostics

The app automatically retries transient TV disconnections with increasing intervals (up to 30 seconds); choosing **Disconnect** disables retries until **Reconnect** or connecting to a profile. See **Options > Connection diagnostics** for connection state, timing, next retry, last error, and a Copy diagnostics action. Pairing tokens are excluded.

Hold VOL or CH arrows to repeat commands after a short delay, or click once to send a single step. The repeat sender permits only one in-flight command to avoid excessive network traffic.

## Favorite apps, keypad, and sleep timer

Open **Apps > Manage favorites** or **Options > Favorite apps** to add up to eight custom streaming shortcuts per TV profile. Enter a name and a numeric Samsung Tizen app ID. App launch support is firmware-dependent, and an accepted request does not prove that the TV opened the app. The dedicated Netflix button remains available.

Use **Options > Numeric channel keypad** for digits 0–9 and Enter; TV support varies by source and application.

Use **Options > Set sleep timer** to schedule one power-toggle command in 1–240 minutes, or cancel a running timer in the same menu. This does not guarantee power-off: if the TV is already off, a power toggle may turn it on. The timer is canceled when the remote application exits and will not fire while it is closed.

## Controls

| UI | Samsung command |
| --- | --- |
| Power (while connected) | `KEY_POWER` (Samsung power toggle) |
| Power (while disconnected, with MAC configured) | Wake-on-LAN broadcast |
| Input | `KEY_SOURCE` |
| Apps / Home | `KEY_HOME` |
| Netflix | Launch app `3201907018807` over REST, with WebSocket `ed.apps.launch` fallback |
| Up / Down / Left / Right | `KEY_UP`, `KEY_DOWN`, `KEY_LEFT`, `KEY_RIGHT` |
| OK | `KEY_ENTER` |
| Back | `KEY_RETURN` |
| Volume +/- and Mute | `KEY_VOLUP`, `KEY_VOLDOWN`, `KEY_MUTE` |
| Channel up/down | `KEY_CHUP`, `KEY_CHDOWN` |
| Info | `KEY_INFO` |
| Rewind / Play / Fast-forward | `KEY_REWIND`, `KEY_PLAY`, `KEY_FF` |
| Options > Pause playback | `KEY_PAUSE` |
| Options > Samsung Menu | `KEY_MENU` |

Some commands may be unsupported by specific Tizen versions or apps. The central Play button sends `KEY_PLAY` rather than an automatically tracked play/pause toggle.

Click the volume up/down arrows to send `KEY_VOLUP` or `KEY_VOLDOWN` one step at a time. With the remote focused, hover over the volume control and scroll the mouse wheel to adjust volume (up to three steps per wheel event). The TV does not report its current volume to the app.

### Windows tray and window options

Minimizing the remote hides it in the Windows system tray instead of leaving an icon on the taskbar. Left-click the tray icon to restore the window. Right-click the tray icon for **Open Clean TV Remote** and **Exit**. Clicking the window's close button still exits the application.

Select **Options > Always on top** to keep the remote over other windows. This setting is saved in your existing local settings file and is off by default.

### Windows keyboard shortcuts

Focus the remote window, then use arrow keys to navigate; Enter for OK; Escape or Backspace for Back; Home for Samsung Home; plus/minus for volume; M for mute; Page Up/Down for channels; Space for Play. Open **Options > Keyboard shortcuts** for the in-app shortcut guide.

## Troubleshooting

- **Can't connect:** Verify the TV IPv4 address, that it is on, and that both devices are on the same LAN. Confirm your TV authorized the app. Check router isolation and firewall settings.
- **Pairing prompt doesn't appear:** Open **Options > Forget pairing**, then connect again and accept the TV prompt. Check TV device permission settings.
- **Connected but no response:** Try Home or Volume Up. Some Samsung key codes aren't accepted by all firmware versions.
- **Text does not appear:** Focus a native Samsung text field with the TV keyboard visible, then use **Type**. Many third-party streaming search keyboards, including Netflix and YouTube, do not accept this command.
- **Netflix doesn't open:** Ensure Netflix is installed, the TV is on, and local app launching is supported by its firmware. The request may succeed without the TV opening the app.
- **Unexpected disconnect:** The remote retries lost connections with a 2–30 second backoff. You can select **Options > Reconnect** to retry immediately. Choosing **Disconnect** stops automatic retries until manually reconnected.
- **Minimized window isn't visible:** Find Clean TV Remote in the Windows notification area and left-click its icon. Right-click for Exit.
- **Power button doesn't turn off the TV:** With the TV on, verify the app is connected and Home or Volume works. The button now sends `KEY_POWER`, which is more widely supported than `KEY_POWEROFF`, but model support still varies.
- **Wake fails:** Check that the configured MAC matches the TV's active Wi-Fi/Ethernet adapter, enable Power On with Mobile (if available), and try Ethernet. A Wake-on-LAN packet being sent does not mean the TV received or accepted it.

## Security and privacy

The app sends commands only to the entered local TV IP. Wake-on-LAN sends a local network broadcast. TV settings and the authentication token are stored in `%APPDATA%\CleanTVRemote\settings.json`. Anyone who obtains the token and can access the TV's network may be able to control it. The secure port 8002 connection uses the TV's self-signed certificate without validating its identity; use only on a trusted home network. Port 8001 is unencrypted.

Diagnostic logs are stored in `%APPDATA%\CleanTVRemote\app.log`. They include connection attempts and error details, but do not record the saved pairing token. Use **Options > Open diagnostic log** to view the file before sharing it.

## Project structure

- `MainForm.cs`: action routing, status handling, pairing and extra actions
- `RemoteSurface.cs`: scalable dark remote UI, painting, hit targets and keyboard support
- `ConnectionDialog.cs`: TV connection and pairing settings dialog
- `TextInputDialog.cs`: local text-entry dialog for compatible Samsung TV input fields
- `SamsungRemoteClient.cs`: existing Samsung WebSocket commands and Wake-on-LAN implementation
- `AppSettings.cs`: existing local settings persistence
- `AppLog.cs`: local diagnostic logging with pairing-token redaction
- `build-windows.bat`: Windows x64 build

## Validation status

The files have been reviewed structurally, but a native Windows build and connection to the physical Samsung TV have **not** been verified in this environment. Build and exercise the controls on Windows before treating this as a confirmed working release.

This is a community application independent of Samsung and Syntrix.
