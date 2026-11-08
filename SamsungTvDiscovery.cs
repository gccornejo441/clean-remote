using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CleanTVRemote;

internal sealed record DiscoveredSamsungTv(string Name, string Ip)
{
    public string Display => $"{Name} ({Ip})";
}

internal static class SamsungTvDiscovery
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.255.250");

    public static async Task<IReadOnlyList<DiscoveredSamsungTv>> FindAsync()
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        using (var udp = new UdpClient(AddressFamily.InterNetwork))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(2600)))
        {
            string request =
                "M-SEARCH * HTTP/1.1\r\n" +
                "HOST: 239.255.255.250:1900\r\n" +
                "MAN: \"ssdp:discover\"\r\n" +
                "MX: 2\r\n" +
                "ST: ssdp:all\r\n\r\n";

            byte[] packet = Encoding.ASCII.GetBytes(request);
            await udp.SendAsync(packet, packet.Length, new IPEndPoint(MulticastAddress, 1900));

            try
            {
                while (candidates.Count < 16)
                {
                    UdpReceiveResult response = await udp.ReceiveAsync(timeout.Token);
                    if (IsPrivateIpv4(response.RemoteEndPoint.Address))
                        candidates.Add(response.RemoteEndPoint.Address.ToString());
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        Task<DiscoveredSamsungTv?>[] probes = candidates.Select(ip => ProbeAsync(ip, http)).ToArray();
        DiscoveredSamsungTv?[] results = await Task.WhenAll(probes);
        return results.Where(device => device is not null)
            .Cast<DiscoveredSamsungTv>()
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<DiscoveredSamsungTv?> ProbeAsync(string ip, HttpClient http)
    {
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using HttpResponseMessage response = await http.GetAsync($"http://{ip}:8001/api/v2/", cancellation.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            string content = await response.Content.ReadAsStringAsync(cancellation.Token);
            if (!content.Contains("Samsung", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("Tizen", StringComparison.OrdinalIgnoreCase))
                return null;

            using JsonDocument document = JsonDocument.Parse(content);
            string name = "Samsung TV";
            if (document.RootElement.TryGetProperty("device", out JsonElement device) &&
                device.ValueKind == JsonValueKind.Object &&
                device.TryGetProperty("name", out JsonElement nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
                name = nameElement.GetString() ?? name;

            return new DiscoveredSamsungTv(name, ip);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return null;
        }
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        byte[] octets = address.GetAddressBytes();
        return octets[0] == 10 ||
            octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31 ||
            octets[0] == 192 && octets[1] == 168 ||
            octets[0] == 169 && octets[1] == 254;
    }
}
