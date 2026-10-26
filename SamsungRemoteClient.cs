using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CleanTVRemote;

internal sealed class SamsungRemoteClient : IDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private ClientWebSocket? _socket;
    private IPAddress? _connectedTvAddress;
    private CancellationTokenSource? _receiveCancellation;

    public event Action<string>? StatusChanged;
    public event Action<string>? PairingTokenChanged;

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public async Task<string?> ConnectAsync(string tvIp, bool secure, string? savedToken)
    {
        Disconnect();

        if (!IPAddress.TryParse(tvIp, out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Enter the TV's IPv4 address, such as 192.168.1.50.");
        }

        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        if (secure)
        {
            socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        string name = Convert.ToBase64String(Encoding.UTF8.GetBytes("Clean TV Remote"));
        string protocol = secure ? "wss" : "ws";
        int port = secure ? 8002 : 8001;
        string url = $"{protocol}://{address}:{port}/api/v2/channels/samsung.remote.control?name={Uri.EscapeDataString(name)}";
        AppLog.Info($"Starting {(secure ? "secure" : "legacy")} TV connection to {address}:{port}.");

        if (!string.IsNullOrWhiteSpace(savedToken))
        {
            url += $"&token={Uri.EscapeDataString(savedToken)}";
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));

        try
        {
            StatusChanged?.Invoke("Connecting to TV…");
            await socket.ConnectAsync(new Uri(url), timeout.Token);
            StatusChanged?.Invoke("Approve 'Clean TV Remote' on your TV if prompted…");

            while (socket.State == WebSocketState.Open)
            {
                string incoming = await ReceiveTextAsync(socket, timeout.Token);
                using var document = JsonDocument.Parse(incoming);
                JsonElement root = document.RootElement;
                string? eventName = root.TryGetProperty("event", out JsonElement eventNode)
                    ? eventNode.GetString()
                    : null;

                if (eventName == "ms.channel.connect")
                {
                    string? token = GetToken(root);
                    _socket = socket;
                    _connectedTvAddress = address;
                    _receiveCancellation = new CancellationTokenSource();
                    _ = ReceiveLoopAsync(socket, _receiveCancellation.Token);
                    AppLog.Info("TV connection established.");
                    StatusChanged?.Invoke("Connected to TV");
                    return token;
                }

                if (eventName == "ms.error")
                {
                    throw new InvalidOperationException("The TV reported a connection error. Check its permission settings.");
                }
            }

            throw new IOException("The TV closed the pairing connection.");
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            throw new TimeoutException("Connection timed out. Verify the TV IP and approve the request on the TV.");
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async Task SendKeyAsync(string key)
    {
        await _sendGate.WaitAsync();

        try
        {
            ClientWebSocket? socket = _socket;
            if (socket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("Not connected. Turn on the TV and click Connect.");
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                method = "ms.remote.control",
                @params = new
                {
                    Cmd = "Click",
                    DataOfCmd = key,
                    Option = "false",
                    TypeOfRemote = "SendRemoteKey"
                }
            });

            await socket.SendAsync(
                new ArraySegment<byte>(payload),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public async Task LaunchAppAsync(string appId)
    {
        if (string.IsNullOrEmpty(appId) || !appId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Provide a numeric Samsung Tizen application ID.", nameof(appId));
        }

        IPAddress? address = _connectedTvAddress;
        if (!IsConnected || address is null)
        {
            throw new InvalidOperationException("Not connected. Turn on the TV and connect before launching an app.");
        }

        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
        {
            try
            {
                using HttpResponseMessage response = await http.PostAsync(
                    $"http://{address}:8001/api/v2/applications/{appId}", null);
                if (response.IsSuccessStatusCode)
                {
                    AppLog.Info($"Samsung REST accepted app launch request for {appId}.");
                    return;
                }

                AppLog.Info($"Samsung REST returned {(int)response.StatusCode} for {appId}; trying WebSocket.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                AppLog.Info($"Samsung REST app launch unavailable: {ex.Message}. Trying WebSocket.");
            }
        }

        await _sendGate.WaitAsync();
        try
        {
            ClientWebSocket? socket = _socket;
            if (socket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("TV disconnected before the app could be launched.");
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                method = "ms.channel.emit",
                @params = new
                {
                    @event = "ed.apps.launch",
                    to = "host",
                    data = new { appId, action_type = "DEEP_LINK" }
                }
            });

            await socket.SendAsync(
                new ArraySegment<byte>(payload),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            AppLog.Info($"Samsung WebSocket app launch request sent for {appId}.");
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public static async Task WakeAsync(string macAddress)
    {
        string normalized = new string(macAddress.Where(Uri.IsHexDigit).ToArray());

        if (normalized.Length != 12)
        {
            throw new ArgumentException("Enter the TV's 12-digit MAC address to use Wake-on-LAN.");
        }

        byte[] mac = Convert.FromHexString(normalized);
        byte[] packet = new byte[102];
        Array.Fill(packet, (byte)0xFF, 0, 6);

        for (int i = 6; i < packet.Length; i += 6)
        {
            Buffer.BlockCopy(mac, 0, packet, i, 6);
        }

        using var sender = new UdpClient();
        sender.EnableBroadcast = true;
        await sender.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
    }

    public void Disconnect()
    {
        ClientWebSocket? oldSocket = _socket;
        _socket = null;
        _connectedTvAddress = null;
        _receiveCancellation?.Cancel();
        _receiveCancellation?.Dispose();
        _receiveCancellation = null;
        oldSocket?.Dispose();
    }

    public void Dispose()
    {
        Disconnect();
        _sendGate.Dispose();
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                string incoming = await ReceiveTextAsync(socket, cancellationToken);
                using var document = JsonDocument.Parse(incoming);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("event", out JsonElement eventNode) &&
                    eventNode.GetString() == "ms.channel.connect")
                {
                    string? token = GetToken(root);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        PairingTokenChanged?.Invoke(token);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (WebSocketException ex)
        {
            AppLog.Error("TV WebSocket receive loop failed.", ex);
        }
        catch (IOException ex)
        {
            AppLog.Info($"TV connection closed: {ex.Message}");
        }
        catch (JsonException)
        {
        }
        finally
        {
            if (ReferenceEquals(_socket, socket))
            {
                _socket = null;
                _connectedTvAddress = null;
                StatusChanged?.Invoke("Disconnected — click Connect to reconnect");
            }

            socket.Dispose();
        }
    }

    private static string? GetToken(JsonElement root)
    {
        if (!root.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("token", out JsonElement token))
        {
            return null;
        }

        return token.ValueKind == JsonValueKind.String ? token.GetString() : token.ToString();
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        using var stream = new MemoryStream();

        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new IOException("TV disconnected.");
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new IOException("Unexpected response from TV.");
            }

            stream.Write(buffer, 0, result.Count);

            if (stream.Length > 131072)
            {
                throw new IOException("TV response exceeded allowed size.");
            }

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
