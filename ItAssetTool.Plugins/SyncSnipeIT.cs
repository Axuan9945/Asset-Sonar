using ItAssetTool.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class SyncSnipeIT : ISyncPlugin
{
    public string Name => "同步到 Snipe-IT";

    private HttpClient _httpClient = new();
    private Action<string> _log = Console.WriteLine;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task SyncAsync(List<HardwareInfo> data, SyncConfig config, Action<string> logCallback)
    {
        _log = logCallback;

        if (string.IsNullOrEmpty(config.ApiKey) || (string.IsNullOrEmpty(config.InternalUrl) && string.IsNullOrEmpty(config.ExternalUrl)))
        {
            _log("❌ 错误：请在配置中填写完整的 Snipe-IT URL 和 API 密钥。");
            return;
        }

        var baseUrl = await DetermineActiveUrl(config.InternalUrl, config.ExternalUrl, config.ApiKey);
        if (string.IsNullOrEmpty(baseUrl))
        {
            _log("❌ 错误：内网和外网URL都无法连接，同步任务中止。");
            return;
        }
        _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _log($"--- 开始同步资产到 Snipe-IT ({baseUrl}) ---");

        var mainAssetData = data.FirstOrDefault(item => item.Category == "主板/整机");
        if (mainAssetData == null) { _log("❌ 错误：未找到有效的主板/整机信息，任务中止。"); return; }

        // 使用传入的 config 对象获取 ID 映射
        var otherAssets = data.Where(d => config.CATEGORY_ID_MAP.ContainsKey(d.Category ?? "") && d.Category != "主板/整机").ToList();
        var components = data.Where(d => config.COMPONENT_CATEGORY_ID_MAP.ContainsKey(d.Category ?? "")).ToList();
        var accessories = data.Where(d => config.ACCESSORY_CATEGORY_ID_MAP.ContainsKey(d.Category ?? "")).ToList();

        var mainAssetId = await SyncAsset(mainAssetData, config); // 传递 config
        if (mainAssetId == null) { _log("❌ 无法获取主资产ID，同步中止。"); return; }

        var userId = await GetOrCreateUserAsync(config);

        foreach (var asset in otherAssets)
        {
            var assetId = await SyncAsset(asset, config); // 传递 config
            if (assetId != null && userId != null)
            {
                // 将 checkoutType 改为 "user"，因为目标是用户
                await CheckoutAssetToUserAsync(assetId.Value, userId.Value, "user"); // <--- 修改点
            }
        }

        await SyncComponentsAsync(components, mainAssetId.Value, config); // 传递 config

        if (userId != null)
        {
            await SyncAccessoriesAsync(accessories, userId.Value, config); // 传递 config
        }

        if (userId != null)
        {
            // 确保主资产也被签出给用户
            await CheckoutAssetToUserAsync(mainAssetId.Value, userId.Value, "user");
        }

        await UpdateAssetNotes(mainAssetId.Value, data, mainAssetData.SerialNumber);

        _log("\n--- ✅ 所有资产同步任务完成 ---");
    }

    #region Private Helper Methods

    private async Task SyncComponentsAsync(List<HardwareInfo> components, int mainAssetId, SyncConfig config)
    {
        var componentGroups = components
            .GroupBy(c => new { c.Model, c.Brand, c.Category })
            .Select(g => new
            {
                ComponentData = g.First(),
                Count = g.Count()
            });

        _log($"\n--- 正在处理 {componentGroups.Count()} 类独特的组件 ---");

        foreach (var group in componentGroups)
        {
            var componentData = group.ComponentData;
            var scannedCount = group.Count;

            if (componentData.Category == null || !config.COMPONENT_CATEGORY_ID_MAP.TryGetValue(componentData.Category, out var categoryId)) continue;

            _log($"\n--- 正在处理组件: {componentData.Model} (扫描到数量: {scannedCount}) ---");

            var manufacturer = await GetOrCreateByNameAsync(componentData.Brand ?? "未知制造商", "manufacturers");

            var componentPayload = new
            {
                name = componentData.Model,
                qty = scannedCount,
                category_id = categoryId,
                manufacturer_id = manufacturer?.GetProperty("id").GetInt32()
            };

            var component = await GetOrCreateByNameAsync(componentData.Model ?? "未知型号", "components", componentPayload);
            if (component == null) continue;

            var componentId = component.Value.GetProperty("id").GetInt32();
            _log($"  -> 正在将 {scannedCount} 个 '{componentData.Model}' 组件关联到主资产 (ID: {mainAssetId})");

            // 将组件签出到 *资产* (assigned_type = asset)
            var checkoutPayload = new { assigned_to = mainAssetId, assigned_type = "asset", assigned_qty = scannedCount };
            await ApiRequestAsync(HttpMethod.Post, $"components/{componentId}/checkout", checkoutPayload);
        }
    }

    private async Task<string?> DetermineActiveUrl(string? internalUrl, string? externalUrl, string apiKey)
    {
        using var tempClient = new HttpClient();
        tempClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        tempClient.Timeout = TimeSpan.FromSeconds(3);
        if (!string.IsNullOrEmpty(internalUrl))
        {
            _log($"  -> 正在尝试连接内网URL: {internalUrl}...");
            try
            {
                var response = await tempClient.GetAsync($"{internalUrl.TrimEnd('/')}/api/v1/statuslabels");
                if (response.IsSuccessStatusCode)
                {
                    _log("  -> ✅ 内网URL连接成功。");
                    return internalUrl;
                }
            }
            catch (Exception ex) { _log($"  -> ⚠️ 内网URL连接失败: {ex.Message}"); }
        }
        if (!string.IsNullOrEmpty(externalUrl))
        {
            _log($"  -> 正在尝试连接外网URL: {externalUrl}...");
            try
            {
                var response = await tempClient.GetAsync($"{externalUrl.TrimEnd('/')}/api/v1/statuslabels");
                if (response.IsSuccessStatusCode)
                {
                    _log("  -> ✅ 外网URL连接成功。");
                    return externalUrl;
                }
            }
            catch (Exception ex) { _log($"  -> ❌ 外网URL连接失败: {ex.Message}"); }
        }
        return null;
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
            if (!response.IsSuccessStatusCode)
            {
                _log($"  -> ❌ API 请求失败 ({response.StatusCode}): {content}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(content)) return JsonSerializer.Deserialize<JsonElement>("{}"); // 返回空对象而非null
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (Exception ex)
        {
            _log($"  -> ❌ API 请求异常: {ex.Message}");
            return null;
        }
    }
    private async Task<int?> GetOrCreateUserAsync(SyncConfig config)
    {
        if (string.IsNullOrEmpty(config.AssignUser) || string.IsNullOrEmpty(config.AssignName))
        {
            _log("  -> ⚠️ 未提供用户名或姓名，跳过用户创建和资产签出。");
            return null;
        }
        _log($"\n--- 正在处理用户: {config.AssignName} ---");
        var dept = await GetOrCreateByNameAsync(config.Department ?? "Default Department", "departments");
        if (dept == null) return null;
        var userPayload = new
        {
            username = config.AssignUser,
            first_name = config.AssignName,
            password = config.AssignPassword,
            password_confirmation = config.AssignPassword,
            department_id = dept.Value.GetProperty("id").GetInt32(),
            email = $"{config.AssignUser}@{config.EmailSuffix}",
            activated = true
        };
        var user = await GetOrCreateByNameAsync(config.AssignUser, "users", userPayload, "username");
        return user?.GetProperty("id").GetInt32();
    }
    private async Task SyncAccessoriesAsync(List<HardwareInfo> accessories, int userId, SyncConfig config)
    {
        _log($"\n--- 正在处理 {accessories.Count} 个配件 ---");
        foreach (var accessoryData in accessories)
        {
            if (accessoryData.Category == null || !config.ACCESSORY_CATEGORY_ID_MAP.TryGetValue(accessoryData.Category ?? "", out var categoryId)) continue;
            var manufacturer = await GetOrCreateByNameAsync(accessoryData.Brand ?? "未知制造商", "manufacturers");
            var accessoryPayload = new
            {
                name = accessoryData.Model,
                category_id = categoryId,
                manufacturer_id = manufacturer?.GetProperty("id").GetInt32(),
                qty = 1
            };
            var accessory = await GetOrCreateByNameAsync(accessoryData.Model ?? "未知型号", "accessories", accessoryPayload);
            if (accessory == null) continue;
            var accessoryId = accessory.Value.GetProperty("id").GetInt32();
            // 配件签出给用户，所以 checkoutType 是 "accessory"
            await CheckoutAssetToUserAsync(accessoryId, userId, "accessory"); // <--- 修改点
        }
    }

    // --- 核心修正：CheckoutAssetToUserAsync 方法 ---
    private async Task CheckoutAssetToUserAsync(int assetOrAccessoryId, int userId, string checkoutType)
    {
        // 确定API端点：hardware 用于资产，accessories 用于配件
        var endpoint = (checkoutType == "user") ? "hardware" : "accessories"; // "asset" 类型也使用 "hardware" 端点
        var assetTypeName = (checkoutType == "accessory") ? "配件" : "资产"; // 用于日志

        _log($"  -> 正在将 {assetTypeName} (ID: {assetOrAccessoryId}) 签出给用户 (ID: {userId})");

        // 检查资产/配件是否已被签出
        var details = await ApiRequestAsync(HttpMethod.Get, $"{endpoint}/{assetOrAccessoryId}");
        if (details?.TryGetProperty("assigned_to", out var assignedTo) == true && assignedTo.ValueKind != JsonValueKind.Null)
        {
            // 检查签出类型是否为用户，以及用户ID是否匹配
            string? assignedToType = null;
            // Snipe-IT v6+ 使用 assigned_to.type, 旧版本可能不同或不存在
            if (assignedTo.TryGetProperty("type", out var typeProp))
            {
                assignedToType = typeProp.GetString();
            }
            // 有些旧版本可能在资产本身上有 type 字段
            else if (details?.TryGetProperty("type", out typeProp) == true)
            {
                assignedToType = typeProp.GetString();
            }

            if (assignedToType == "user" && assignedTo.TryGetProperty("id", out var assignedId) && assignedId.GetInt32() == userId)
            {
                _log($"  -> ℹ️ {assetTypeName} (ID: {assetOrAccessoryId}) 已签出给此用户 (ID: {userId})，跳过操作。");
                return;
            }
            else
            {
                _log($"  -> ℹ️ {assetTypeName} (ID: {assetOrAccessoryId}) 已被签出给其他对象/用户。将尝试覆盖签出...");
            }
        }

        // 构建正确的签出 Payload：始终签出给 "user" 类型，并提供用户ID
        var checkoutPayload = new
        {
            checkout_to_type = "user",
            assigned_user = userId
            // 不需要 assigned_asset
        };

        // 发送签出请求
        var response = await ApiRequestAsync(HttpMethod.Post, $"{endpoint}/{assetOrAccessoryId}/checkout", checkoutPayload);

        // 添加更详细的成功/失败日志
        if (response?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
        {
            _log($"    - ✅ 成功将 {assetTypeName} (ID: {assetOrAccessoryId}) 签出给用户 (ID: {userId})。");
        }
        else
        {
            var messages = response?.TryGetProperty("messages", out var msg) == true ? msg.ToString() : "未知错误";
            _log($"    - ❌ 将 {assetTypeName} (ID: {assetOrAccessoryId}) 签出给用户 (ID: {userId}) 失败。原因: {messages}");
        }
    }
    // --- 修正结束 ---

    private async Task UpdateAssetNotes(int assetId, List<HardwareInfo> allData, string? mainSerial)
    {
        _log("\n--- 正在生成并更新主资产备注信息 ---");
        var notes = new StringBuilder("\n--- 自动同步的硬件信息 ---\n");
        foreach (var item in allData)
        {
            var line = $"[{item.Category}] {item.Brand} {item.Model}".Trim();
            if (!string.IsNullOrEmpty(item.Size) && item.Size != "N/A") line += $" ({item.Size})";
            if (!string.IsNullOrEmpty(item.SerialNumber) && item.SerialNumber != "N/A" && item.SerialNumber != mainSerial)
            {
                line += $" - SN: {item.SerialNumber}";
            }
            notes.AppendLine(line);
        }
        var payload = new { notes = notes.ToString() };
        await ApiRequestAsync(new HttpMethod("PATCH"), $"hardware/{assetId}", payload);
        _log("  -> ✅ 备注信息更新成功。");
    }
    private async Task<JsonElement?> GetOrCreateByNameAsync(string name, string endpoint, object? creationPayload = null, string searchField = "name")
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _log($"  -> 正在按名称查询 {endpoint.TrimEnd('s')}: {name}...");
        var searchResult = await ApiRequestAsync(HttpMethod.Get, $"{endpoint}?search={Uri.EscapeDataString(name)}");
        if (searchResult?.TryGetProperty("total", out var total) == true && total.GetInt32() > 0)
        {
            foreach (var item in searchResult.Value.GetProperty("rows").EnumerateArray())
            {
                if (item.TryGetProperty(searchField, out var fieldValue) && fieldValue.GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    _log($"  -> ✅ 已找到 (ID: {item.GetProperty("id")})");
                    return item;
                }
            }
        }
        _log($"  -> 不存在，正在创建...");
        var payload = creationPayload ?? new { name };
        var createResult = await ApiRequestAsync(HttpMethod.Post, endpoint, payload);
        if (createResult?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
        {
            if (createResult.Value.TryGetProperty("payload", out var newPayload))
            {
                _log($"  -> ✅ 成功创建 (新 ID: {newPayload.GetProperty("id")})");
                return newPayload;
            }
            else if (createResult.Value.TryGetProperty("id", out _))
            { // 有些API直接返回对象
                _log($"  -> ✅ 成功创建 (新 ID: {createResult.Value.GetProperty("id")})");
                return createResult.Value;
            }
        }
        _log($"  -> ❌ 创建失败: {createResult}");
        return null;
    }
    private async Task<string?> GetNextAssetTagAsync(string prefix)
    {
        _log($"  -> 正在为前缀 '{prefix}' 查询下一个可用资产标签...");
        var searchResult = await ApiRequestAsync(HttpMethod.Get, $"hardware?search={prefix}&sort=asset_tag&order=desc&limit=1");
        if (searchResult?.TryGetProperty("total", out var total) == true && total.GetInt32() > 0)
        {
            var rows = searchResult.Value.GetProperty("rows");
            if (rows.GetArrayLength() > 0) // 添加检查确保 rows 非空
            {
                var lastTag = rows[0].GetProperty("asset_tag").GetString() ?? "";
                var match = Regex.Match(lastTag, @"(\d+)$");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out var num))
                    {
                        var nextTag = $"{prefix}{(num + 1):D4}";
                        _log($"  -> ✅ 下一个可用资产标签是: {nextTag}");
                        return nextTag;
                    }
                }
            }
        }
        _log("  -> 未找到匹配的资产或无法解析编号，将从 0001 开始。");
        return $"{prefix}0001";
    }
    private async Task<int?> SyncAsset(HardwareInfo assetData, SyncConfig config)
    {
        var serial = assetData.SerialNumber;
        if (string.IsNullOrEmpty(serial) || serial.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            _log($"  -> ⚠️ 跳过资产 '{assetData.Model}'，因为它没有有效的序列号。");
            return null;
        }
        _log($"\n--- 正在处理独立资产, 序列号: {serial} ---");
        var manufacturer = await GetOrCreateByNameAsync(assetData.Brand ?? "未知制造商", "manufacturers");
        if (manufacturer == null) return null;
        var categoryName = assetData.Category == "主板/整机" ? "台式机" : assetData.Category;

        if (categoryName == null || !config.CATEGORY_ID_MAP.TryGetValue(categoryName, out var categoryId))
        {
            _log($"  -> ❌ 错误：未在 CATEGORY_ID_MAP 中配置资产类别 '{categoryName}' 的ID。");
            return null;
        }

        var modelPayload = new { name = assetData.Model, category_id = categoryId, manufacturer_id = manufacturer.Value.GetProperty("id").GetInt32() };
        var model = await GetOrCreateByNameAsync(assetData.Model ?? "未知型号", "models", modelPayload);
        if (model == null) return null;
        var existingAsset = await ApiRequestAsync(HttpMethod.Get, $"hardware/byserial/{Uri.EscapeDataString(serial)}");
        if (existingAsset?.TryGetProperty("total", out var total) == true && total.GetInt32() > 0)
        {
            var rows = existingAsset.Value.GetProperty("rows");
            if (rows.GetArrayLength() > 0) // 添加检查确保 rows 非空
            {
                var assetId = rows[0].GetProperty("id").GetInt32();
                _log($"  -> ✅ 资产 '{assetData.Model}' 已存在 (ID: {assetId})。");
                return assetId;
            }
        }

        _log($"  -> 资产 '{assetData.Model}' 不存在，正在创建...");
        var categoryCode = config.CATEGORY_CODE_MAP.GetValueOrDefault(categoryName, "AST");
        var tagPrefix = $"{config.ASSET_TAG_PREFIX}-{categoryCode}-";

        var nextTag = await GetNextAssetTagAsync(tagPrefix);
        var assetPayload = new
        {
            model_id = model.Value.GetProperty("id").GetInt32(),
            serial = serial,
            name = $"{categoryName} - {Environment.MachineName}",
            status_id = 2,
            asset_tag = nextTag
        };
        var createResult = await ApiRequestAsync(HttpMethod.Post, "hardware", assetPayload);
        if (createResult?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
        {
            if (createResult.Value.TryGetProperty("payload", out var payloadElement) && payloadElement.TryGetProperty("id", out var idElement))
            {
                var newAssetId = idElement.GetInt32();
                _log($"  -> ✅ 成功创建资产 (新 ID: {newAssetId})");
                return newAssetId;
            }
        }
        _log($"  -> ❌ 创建资产失败: {createResult}");
        return null;

    }
    #endregion
}