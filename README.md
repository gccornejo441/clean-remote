# Clean TV Remote

A compact Windows remote for Samsung Tizen TVs, including the Samsung TU690T (`UN58TU690TFXZA`). Built with C# and Windows Forms on .NET 10. It runs on your home network without a Samsung account, cloud backend, telemetry, or third-party UI packages.

## Redesigned remote

The main window uses a dark, narrow layout inspired by the provided remote screenshot:

- Dark charcoal background, soft circular buttons, purple OK button, and red power button
- TV name and connection indicator across the top
- Power, Input, Apps, Netflix, and Keyboard shortcut helper
- Large circular four-way navigation pad with OK in the center
- Back, Home, and Options shortcuts
- A drag-to-adjust volume slider and channel rocker, with mute and info controls
- Rewind, Play, and Fast-forward controls
- Hover and press feedback, responsive layout, and Windows keyboard navigation

**Apps** opens Samsung Home (`KEY_HOME`). **Netflix** requests a launch of Samsung Tizen app `3201907018807` using the local REST application endpoint, with WebSocket fallback. The app must be installed on the TV, and some firmware ignores remote app launches. A request being accepted does not confirm Netflix opened. The **Keyboard** button shows supported PC-to-TV remote shortcuts; typing arbitrary text into Samsung TV apps is **not** currently implemented. **Options** opens additional actions such as Samsung Menu, Pause, Wake TV, reconnect, and pairing management.

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

If you want to wake your TV from standby, also provide its MAC address in the connection settings. Wake-on-LAN may not work over Wi-Fi or when the TV enters deep standby; it is not a guaranteed power-on method.

## Controls

| UI | Samsung command |
| --- | --- |
| Power (while connected) | `KEY_POWEROFF` |
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

The volume slider sends `KEY_VOLUP` or `KEY_VOLDOWN` steps as you drag it. The TV does not report its current volume to the app, so the slider adjusts relatively and does not display the TV's actual level.

### Windows keyboard shortcuts

Focus the remote window, then use arrow keys to navigate; Enter for OK; Escape or Backspace for Back; Home for Samsung Home; plus/minus for volume; M for mute; Page Up/Down for channels; Space for Play. Click **Keyboard** for the in-app shortcut guide.

## Troubleshooting

- **Can't connect:** Verify the TV IPv4 address, that it is on, and that both devices are on the same LAN. Confirm your TV authorized the app. Check router isolation and firewall settings.
- **Pairing prompt doesn't appear:** Open **Options > Forget pairing**, then connect again and accept the TV prompt. Check TV device permission settings.
- **Connected but no response:** Try Home or Volume Up. Some Samsung key codes aren't accepted by all firmware versions.
- **Netflix doesn't open:** Ensure Netflix is installed, the TV is on, and local app launching is supported by its firmware. The request may succeed without the TV opening the app.
- **Unexpected disconnect:** Use **Options > Reconnect**.
- **Wake fails:** Check MAC address, Power On with Mobile (if available), and try Ethernet.

## Security and privacy

The app sends commands only to the entered local TV IP. Wake-on-LAN sends a local network broadcast. TV settings and the authentication token are stored in `%APPDATA%\CleanTVRemote\settings.json`. Anyone who obtains the token and can access the TV's network may be able to control it. The secure port 8002 connection uses the TV's self-signed certificate without validating its identity; use only on a trusted home network. Port 8001 is unencrypted.

Diagnostic logs are stored in `%APPDATA%\CleanTVRemote\app.log`. They include connection attempts and error details, but do not record the saved pairing token. Use **Options > Open diagnostic log** to view the file before sharing it.

## Project structure

- `MainForm.cs`: action routing, status handling, pairing and extra actions
- `RemoteSurface.cs`: scalable dark remote UI, painting, hit targets and keyboard support
- `ConnectionDialog.cs`: TV connection and pairing settings dialog
- `SamsungRemoteClient.cs`: existing Samsung WebSocket commands and Wake-on-LAN implementation
- `AppSettings.cs`: existing local settings persistence
- `AppLog.cs`: local diagnostic logging with pairing-token redaction
- `build-windows.bat`: Windows x64 build

## Validation status

The files have been reviewed structurally, but a native Windows build and connection to the physical Samsung TV have **not** been verified in this environment. Build and exercise the controls on Windows before treating this as a confirmed working release.

This is a community application independent of Samsung and Syntrix.
