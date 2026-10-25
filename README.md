# Clean TV Remote

A small, Windows-only Samsung Tizen TV remote. Built with C# and Windows Forms on .NET 10. No Microsoft Store app, Samsung account, external service, or third-party package is required. Network control stays on your home LAN.

## Compatibility

Designed for Samsung TU690T (`UN58TU690TFXZA`) and related TVs exposing Samsung's local WebSocket remote endpoint. It has **not yet been tested against the physical TV**. Actual availability of commands depends on TV settings and firmware.

## Build a Windows executable

1. Install the [.NET 10 SDK for Windows](https://dotnet.microsoft.com/download/dotnet/10.0) if you do not already have it.
2. Extract the ZIP to a folder on Windows.
3. Double-click `build-windows.bat`.
4. Open `publish\CleanTVRemote.exe`.

The script builds a self-contained single-file Windows x64 application. The resulting EXE is larger than the source files because it includes the .NET runtime. You can copy that EXE to another compatible Windows x64 machine without installing .NET there. The EXE is not code-signed, so Windows may display an unknown-publisher warning; verify the source before running it.

You can also open `CleanTVRemote.csproj` in Visual Studio with the .NET desktop development workload.

## Connect to the Samsung TV

1. Turn the TV on and put your PC and TV on the same home LAN.
2. If your TV exposes these settings, turn on **IP Remote** in **Settings > General > Network > Expert Settings**. Enable **Power On with Mobile** for waking the TV (setting availability varies by firmware).
3. Find the TV's IP address in its network settings or your router's device list.
4. Enter the TV IPv4 address in the app and select **Secure (8002)**.
5. Click **CONNECT** and select **Allow** on the TV's authorization popup.
6. Once connected, use the on-screen buttons. Pairing is saved for subsequent sessions.

If secure port 8002 does not work, test **Legacy (8001)**. Port 8001 sends remote control commands without encryption. Do not use either mode on an untrusted network.

## Buttons

- Navigation: Up, Down, Left, Right, OK, Back, Home
- Sound: Volume +, Volume -, Mute
- TV: Source, Channel +, Channel -, Power Off
- Wake TV: sends a Wake-on-LAN packet using the optional TV MAC address

The **PLAY** button sends `KEY_PLAY`; availability varies by playback app. The Source command may be ignored on some Samsung firmware. Wake-on-LAN is more reliable on Ethernet and may not wake a TV connected via Wi-Fi or one in deep standby. It is not a guarantee the TV turns on.

## Troubleshooting

- **Timeout:** Confirm the TV is powered on, the IP address is correct, and both devices are on the same LAN. Check router client isolation and firewall settings.
- **No pairing popup:** On the TV, review external device/IP remote permissions. Use **Forget pairing** in the app and reconnect.
- **Connected, but no response:** Test Volume Up or Home first. Some key codes are silently ignored on different firmware.
- **TV IP changes:** Reserve an IP address for the TV in your router's DHCP settings.
- **Wake doesn't work:** Enable Power On with Mobile on the TV, use the correct MAC address, and try wired Ethernet.

## Privacy and security

- The app talks only to the configured local TV address and sends optional Wake-on-LAN broadcasts.
- It has no telemetry, cloud login, or background service.
- The local pairing token and the TV's IP/MAC are stored in `%APPDATA%\CleanTVRemote\settings.json`. Protect your Windows account and home network; anyone with the token and LAN access may be able to control the TV.
- Many Samsung TVs use a self-signed TLS certificate on port 8002. The app therefore accepts that certificate for the **configured TV endpoint only**. This permits an on-path device on your LAN to impersonate the TV; use a trusted home network.
- This application is independent of Samsung and Syntrix.

## Limitations

There is no auto-discovery, installer, keyboard remote, app launcher, or automatic reconnect in this first version. Physical-TV interoperability and Windows build output have not yet been verified in this environment.

## Protocol references

- https://image-us.samsung.com/SamsungUS/samsungbusiness/tv-ci-resources/Samsung-IP-Control.pdf
- https://github.com/pkapteijn/samsungtv-rc
- https://github.com/PepperDash/epi-samsung-tizenWebsocket
