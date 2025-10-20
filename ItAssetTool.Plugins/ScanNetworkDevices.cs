using ItAssetTool.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

public class ScanNetworkDevices : INetworkScanPlugin
{
    private readonly ILogger<ScanNetworkDevices> _logger;

    public string Name => "局域网设备扫描";

    public ScanNetworkDevices(ILogger<ScanNetworkDevices> logger)
    {
        _logger = logger;
    }

    public async Task ScanAsync(IProgress<NetworkDevice> progress, CancellationToken cancellationToken)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        _logger.LogInformation("开始局域网设备扫描...");

        NetworkInterface? activeInterface = null;
        IPAddress? ipAddress = null;
        IPAddress? subnetMask = null;
        IPAddress? networkAddress = null;

        try
        {
            activeInterface = GetActiveNetworkInterface();
            if (activeInterface == null) { _logger.LogWarning("未找到活动的网络接口..."); return; }
            var ipProperties = activeInterface.GetIPProperties();
            var ipv4Info = ipProperties.UnicastAddresses.FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork && addr.IPv4Mask != null);
            if (ipv4Info == null) { _logger.LogWarning("活动网络接口 '{Desc}' 无 IPv4 配置。", activeInterface.Description); return; }
            ipAddress = ipv4Info.Address;
            subnetMask = ipv4Info.IPv4Mask;
            networkAddress = GetNetworkAddress(ipAddress, subnetMask);
            _logger.LogInformation("本地 IP: {IPAddress}, 子网掩码: {SubnetMask}, 网络地址: {NetworkAddress}", ipAddress, subnetMask, networkAddress);
        }
        catch (Exception ex) { _logger.LogError(ex, "准备扫描时出错。"); progress.Report(new NetworkDevice { Status = "错误", HostName = $"准备失败: {ex.Message}" }); return; }

        IEnumerable<IPAddress> ipRange;
        try
        {
            ipRange = GetIpRange(networkAddress, subnetMask);
            _logger.LogInformation("将扫描 IP 范围内的 {IpCount} 个地址。", ipRange.Count());
        }
        catch (ArgumentOutOfRangeException argEx) { _logger.LogError(argEx, "计算 IP 范围时出错"); progress.Report(new NetworkDevice { Status = "错误", HostName = $"计算IP范围失败: {argEx.Message}" }); return; }
        catch (Exception ex) { _logger.LogError(ex, "计算 IP 范围时发生未知错误。"); progress.Report(new NetworkDevice { Status = "错误", HostName = $"计算IP范围失败: {ex.Message}" }); return; }


        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 50, CancellationToken = cancellationToken };
        int onlineCount = 0;
        try
        {
            await Parallel.ForEachAsync(ipRange, parallelOptions, async (ip, token) =>
            {
                var device = new NetworkDevice { IpAddress = ip.ToString() };
                using var pinger = new Ping();
                try
                {
                    _logger.LogTrace("Pinging {IPAddress}...", ip);
                    var reply = await pinger.SendPingAsync(ip, 500);

                    if (reply.Status == IPStatus.Success)
                    {
                        Interlocked.Increment(ref onlineCount);
                        device.Status = "在线";
                        device.Latency = reply.RoundtripTime;
                        _logger.LogDebug("设备在线: {IPAddress}, 延迟: {Latency}ms", ip, device.Latency);
                        try
                        {
                            var hostEntryTask = Dns.GetHostEntryAsync(ip);
                            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                            timeoutCts.CancelAfter(TimeSpan.FromSeconds(1));
                            var completedTask = await Task.WhenAny(hostEntryTask, Task.Delay(-1, timeoutCts.Token));
                            if (completedTask == hostEntryTask && !hostEntryTask.IsFaulted) device.HostName = hostEntryTask.Result.HostName;
                            else device.HostName = "N/A";
                        }
                        catch (OperationCanceledException) { device.HostName = "N/A (DNS 超时)"; }
                        catch (SocketException dnsSocEx) { device.HostName = "N/A (DNS 失败)"; _logger.LogDebug(dnsSocEx, "DNS 解析 {IPAddress} 失败", ip); }
                        catch (Exception dnsEx) { device.HostName = "N/A (DNS 错误)"; _logger.LogWarning(dnsEx, "DNS 解析 {IPAddress} 时出错", ip); }
                    }
                    else { device.Status = "未使用"; device.HostName = "-"; _logger.LogTrace("设备未使用: {IPAddress} ({Status})", ip, reply.Status); }
                }
                // --- vvvv CS0168 修正：将 pingEx 传递给 LogTrace vvvv ---
                catch (PingException pingEx) { device.Status = "未使用"; device.HostName = "-"; _logger.LogTrace(pingEx, "Ping {IPAddress} 失败 (PingException)", ip); } // 修正：传递 pingEx
                // --- ^^^^ 修正结束 ^^^^
                catch (OperationCanceledException) when (token.IsCancellationRequested) { _logger.LogInformation("扫描被用户取消(内部)。"); }
                // --- vvvv CS0168 修正：将 ex 传递给 LogError vvvv ---
                catch (Exception ex)
                {
                    device.Status = "错误"; device.HostName = "Ping异常";
                    _logger.LogError(ex, "Ping {IPAddress} 时发生意外错误", ip); // 修正：传递 ex
                }
                // --- ^^^^ 修正结束 ^^^^
                if (!token.IsCancellationRequested) progress.Report(device);
            });
        }
        catch (OperationCanceledException) { _logger.LogInformation("局域网设备扫描被用户取消(外部)。"); }
        // --- vvvv CS0168 修正：将 ex 传递给 LogError vvvv ---
        catch (Exception ex)
        {
            _logger.LogError(ex, "并行扫描过程中发生错误。"); // 修正：传递 ex
            progress.Report(new NetworkDevice { Status = "错误", HostName = $"扫描过程出错: {ex.Message}" });
        }
        // --- ^^^^ 修正结束 ^^^^

        _logger.LogInformation("局域网设备扫描完成。发现 {OnlineCount} 个在线设备。", onlineCount);
    }

    private static NetworkInterface? GetActiveNetworkInterface()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
               .OrderByDescending(i => i.Speed)
               .FirstOrDefault(i => i.OperationalStatus == OperationalStatus.Up &&
                                    (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                                    i.GetIPProperties().GatewayAddresses.Any(g => g?.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork));
        }
        catch (NetworkInformationException netEx) { /* Log.Logger?.Error(netEx, "..."); */ return null; }
        catch (Exception ex) { /* Log.Logger?.Error(ex, "..."); */ return null; }
    }
    private static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ipBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        if (ipBytes.Length != maskBytes.Length) throw new ArgumentException("IP 地址和子网掩码长度不匹配");
        byte[] networkBytes = new byte[ipBytes.Length];
        for (int i = 0; i < ipBytes.Length; i++) { networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]); }
        return new IPAddress(networkBytes);
    }
    private static IEnumerable<IPAddress> GetIpRange(IPAddress networkAddress, IPAddress subnetMask)
    {
        if (networkAddress == null) throw new ArgumentNullException(nameof(networkAddress));
        if (subnetMask == null) throw new ArgumentNullException(nameof(subnetMask));
        byte[] networkBytes = networkAddress.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        if (networkBytes.Length != 4 || maskBytes.Length != 4) throw new ArgumentException("仅支持 IPv4 地址");
        uint firstIpUint = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0);
        uint maskUint = BitConverter.ToUInt32(maskBytes.Reverse().ToArray(), 0);
        uint lastIpUint = firstIpUint | ~maskUint;
        long hostCount = (long)lastIpUint - firstIpUint;
        const long maxHostsToScan = 65536;
        if (hostCount <= 0 || hostCount > maxHostsToScan + 1) { throw new ArgumentOutOfRangeException($"子网过大 ({hostCount - 1} 个主机)，超过扫描限制 ({maxHostsToScan})。"); }
        for (uint ip = firstIpUint + 1; ip < lastIpUint; ip++) { yield return new IPAddress(BitConverter.GetBytes(ip).Reverse().ToArray()); }
    }
}