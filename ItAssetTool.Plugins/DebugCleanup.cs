using ItAssetTool.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class DebugCleanup : IDebugPlugin
{
    public string Name => "Snipe-IT 数据清理";

    private HttpClient _httpClient = new();
    private Action<string> _log = Console.WriteLine;

    public async Task RunCleanupAsync(SyncConfig config, Action<string> logCallback)
    {
        _log = logCallback;

        if (string.IsNullOrEmpty(config.ApiKey) || string.IsNullOrEmpty(config.InternalUrl))
        {
            _log("❌ 错误：请在配置中填写完整的 Snipe-IT 内网 URL 和 API 密钥以执行清理。");
            return;
        }

        var baseUrl = config.InternalUrl;
        _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _log($"\n--- !!! 开始执行 Snipe-IT 数据清理任务 !!! ---");
        _log($"--- !!! 目标服务器: {baseUrl} !!! ---");

        await CheckinAllAssets();
        await CheckinAllAccessories();

        var endpointsToClear = new[]
        {
            "hardware", "accessories", "components", "users",
            "models", "manufacturers", "departments"
        };

        for (int i = 0; i < endpointsToClear.Length; i++)
        {
            await DeleteAllFromEndpoint(endpointsToClear[i], i + 3);
        }

        _log("\n--- ✅ 所有清理任务执行完毕 ---");
    }

    private async Task CheckinAllAssets()
    {
        _log("--- 步骤 1/9: 正在归还所有已借出的资产 (Hardware) ---");
        var result = await ApiRequestAsync(HttpMethod.Get, "hardware?status=deployed&limit=500");
        if (result?.TryGetProperty("rows", out var assets) != true || assets.GetArrayLength() == 0)
        {
            _log("  -> 没有需要归还的资产。");
            return;
        }

        _log($"  -> 发现 {assets.GetArrayLength()} 个已借出的资产，正在逐一归还...");
        foreach (var asset in assets.EnumerateArray())
        {
            var assetId = asset.GetProperty("id").GetInt32();
            var response = await ApiRequestAsync(HttpMethod.Post, $"hardware/{assetId}/checkin", new { note = "调试模式自动归还" });
            if (response?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
            {
                _log($"    - ✅ 已归还资产: {asset.GetProperty("name").GetString()} (Tag: {asset.GetProperty("asset_tag").GetString()})");
            }
        }
    }

    private async Task CheckinAllAccessories()
    {
        _log("--- 步骤 2/9: 正在归还所有已借出的配件 (Accessories) ---");
        var result = await ApiRequestAsync(HttpMethod.Get, "accessories?limit=500");
        if (result?.TryGetProperty("rows", out var accessories) != true || accessories.GetArrayLength() == 0)
        {
            _log("  -> 系统中没有任何配件。");
            return;
        }

        var accessoriesWithCheckout = accessories.EnumerateArray()
            .Where(acc => acc.TryGetProperty("qty", out var qty) && qty.GetInt32() > (acc.TryGetProperty("remaining_qty", out var rem) ? rem.GetInt32() : 0))
            .ToList();

        if (!accessoriesWithCheckout.Any())
        {
            _log("  -> 没有需要归还的配件。");
            return;
        }

        _log($"  -> 发现 {accessoriesWithCheckout.Count} 类已借出的配件，正在处理...");
        foreach (var acc in accessoriesWithCheckout)
        {
            var accId = acc.GetProperty("id").GetInt32();
            var accName = acc.GetProperty("name").GetString();

            var checkedOutResult = await ApiRequestAsync(HttpMethod.Get, $"accessories/{accId}/checkedout");
            if (checkedOutResult?.TryGetProperty("rows", out var checkoutLogs) != true || checkoutLogs.GetArrayLength() == 0)
            {
                _log($"  -> 警告: 配件 '{accName}' 显示已借出，但无法获取其借出记录，跳过。");
                continue;
            }

            foreach (var log in checkoutLogs.EnumerateArray())
            {
                var logId = log.GetProperty("id").GetInt32();
                var response = await ApiRequestAsync(HttpMethod.Post, $"accessories/{logId}/checkin", new { note = "调试模式自动归还" });
                if (response?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
                {
                    _log($"    - ✅ 已归还配件: {accName}");
                }
            }
        }
    }

    private async Task DeleteAllFromEndpoint(string endpoint, int stepNumber)
    {
        _log($"--- 步骤 {stepNumber}/9: 正在清理 {endpoint} ---");
        var result = await ApiRequestAsync(HttpMethod.Get, $"{endpoint}?limit=500");
        if (result?.TryGetProperty("rows", out var items) != true || items.GetArrayLength() == 0)
        {
            _log($"  -> {endpoint} 中没有需要清理的项目。");
            return;
        }

        _log($"  -> 发现 {items.GetArrayLength()} 个项目需要清理...");
        int count = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null)
            {
                _log("    - ⚠️ 发现一个空的 API 返回项，已跳过。");
                continue;
            }

            var itemId = item.GetProperty("id").GetInt32();
            var itemName = item.TryGetProperty("name", out var name) ? name.GetString() : item.TryGetProperty("username", out var username) ? username.GetString() : $"ID: {itemId}";

            if (endpoint == "users")
            {
                if (itemId == 1) { _log($"  -> ⚠️ 跳过用户 '{itemName}' (ID: 1, 初始管理员)。"); continue; }

                bool isAdmin = item.TryGetProperty("admin", out var adminProp) && adminProp.GetBoolean();

                // vvvv 这是核心修正：在访问 'groups' 的子属性前，先检查它本身是否为 null vvvv
                if (!isAdmin && item.TryGetProperty("groups", out var groups) && groups.ValueKind != JsonValueKind.Null && groups.TryGetProperty("rows", out var groupRows))
                {
                    isAdmin = groupRows.EnumerateArray().Any(g =>
                    {
                        var groupName = g.GetProperty("name").GetString()?.ToLower();
                        return groupName == "admin" || groupName == "super user";
                    });
                }
                // ^^^^ 核心修正结束 ^^^^

                if (isAdmin) { _log($"  -> ⚠️ 跳过用户 '{itemName}' (管理员)。"); continue; }
            }

            var deleteResponse = await ApiRequestAsync(HttpMethod.Delete, $"{endpoint}/{itemId}");
            if (deleteResponse?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
            {
                _log($"    - ✅ 已删除: {itemName}");
                count++;
            }
            else
            {
                var messages = deleteResponse?.TryGetProperty("messages", out var msg) == true ? msg.ToString() : "未知错误";
                _log($"    - ❌ 删除 '{itemName}' 失败。原因: {messages}");
            }
        }
        _log($"--- 清理完成: {endpoint} (共删除 {count}/{items.GetArrayLength()} 个) ---");
    }

    private async Task<JsonElement?> ApiRequestAsync(HttpMethod method, string endpoint, object? payload = null)
    {
        try
        {
            var request = new HttpRequestMessage(method, endpoint);
            if (payload != null)
            {
                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            }
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) { _log($"  -> ❌ API 请求失败 ({response.StatusCode}): {content}"); return null; }
            if (string.IsNullOrWhiteSpace(content)) return JsonSerializer.Deserialize<JsonElement>("{}");
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (Exception ex) { _log($"  -> ❌ API 请求异常: {ex.Message}"); return null; }
    }
}