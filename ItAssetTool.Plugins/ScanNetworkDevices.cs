// In Project: ItAssetTool.Plugins
// File: ScanNetworkDevices.cs
using ItAssetTool.Core;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ItAssetTool.Plugins;

public class ScanNetworkDevices : INetworkScanPlugin
{
    public string Name => "局域网设备扫描";

    public async Task ScanAsync(IProgress<NetworkDevice> progress, CancellationToken cancellationToken)
    {
        var activeInterface = GetActiveNetworkInterface();
        if (activeInterface == null) return;

        var ipProperties = activeInterface.GetIPProperties();
        var ipv4Info = ipProperties.UnicastAddresses
            .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

        if (ipv4Info == null) return;

        var ipAddress = ipv4Info.Address;
        var subnetMask = ipv4Info.IPv4Mask;
        var networkAddress = GetNetworkAddress(ipAddress, subnetMask);

        var ipRange = GetIpRange(networkAddress, subnetMask);

        await Parallel.ForEachAsync(ipRange, new ParallelOptions { MaxDegreeOfParallelism = 50, CancellationToken = cancellationToken }, async (ip, token) =>
        {
            if (token.IsCancellationRequested) return;

            var device = new NetworkDevice { IpAddress = ip.ToString() };
            using var pinger = new Ping();
            try
            {
                var reply = await pinger.SendPingAsync(ip, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    device.Status = "在线";
                    // vvvv 核心修改：记录延迟 vvvv
                    device.Latency = reply.RoundtripTime;
                    // ^^^^ 修改结束 ^^^^
                    try
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(ip);
                        device.HostName = hostEntry.HostName;
                    }
                    catch { device.HostName = "N/A"; }
                }
                else
                {
                    device.Status = "未使用";
                    device.HostName = "-";
                }
            }
            catch (PingException)
            {
                device.Status = "未使用";
                device.HostName = "-";
            }

            progress.Report(device);
        });
    }

    private static NetworkInterface? GetActiveNetworkInterface()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(i => i.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 i.OperationalStatus == OperationalStatus.Up &&
                                 i.GetIPProperties().GatewayAddresses.Any());
    }

    private static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ipBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        byte[] networkBytes = new byte[ipBytes.Length];
        for (int i = 0; i < ipBytes.Length; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }
        return new IPAddress(networkBytes);
    }

    private static IEnumerable<IPAddress> GetIpRange(IPAddress networkAddress, IPAddress subnetMask)
    {
        var networkBytes = networkAddress.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();

        uint firstIp = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0);
        uint mask = BitConverter.ToUInt32(maskBytes.Reverse().ToArray(), 0);
        uint lastIp = firstIp | ~mask;

        for (uint ip = firstIp + 1; ip < lastIp; ip++)
        {
            yield return new IPAddress(BitConverter.GetBytes(ip).Reverse().ToArray());
        }
    }
}