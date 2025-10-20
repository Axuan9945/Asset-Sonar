using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
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
    private readonly ILogger<SyncSnipeIT> _logger; // <-- 添加 logger
    private HttpClient _httpClient = new();
    // 修改 _log 的类型
    private Action<string> _uiLogCallback = Console.WriteLine;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string Name => "同步到 Snipe-IT";

    // 构造函数注入
    public SyncSnipeIT(ILogger<SyncSnipeIT> logger)
    {
        _logger = logger;
    }

    // 内部日志方法，同时记录日志并调用 UI 回调
    private void Log(string message, LogLevel level = LogLevel.Information, Exception? ex = null)
    {
        _logger.Log(level, ex, message);
        // 使用 Action<string> logCallback 将信息传递回 UI
        _uiLogCallback?.Invoke($"[{level.ToString().ToUpper()}] {message}{(ex != null ? $" -> {ex.Message}" : "")}");
    }

    public async Task SyncAsync(List<HardwareInfo> data, SyncConfig config, Action<string> logCallback)
    {
        _uiLogCallback = logCallback; // 保存 UI 回调

        if (config == null)
        {
            Log("配置对象为 null，无法执行同步。", LogLevel.Critical);
            throw new ArgumentNullException(nameof(config));
        }
        if (data == null || !data.Any())
        {
            Log("没有扫描到有效数据，同步任务中止。", LogLevel.Warning);
            return;
        }

        if (string.IsNullOrEmpty(config.ApiKey) || (string.IsNullOrEmpty(config.InternalUrl) && string.IsNullOrEmpty(config.ExternalUrl)))
        {
            Log("错误：请在配置中填写完整的 Snipe-IT URL 和 API 密钥。", LogLevel.Error);
            return;
        }
        // 验证 ID 映射是否至少包含基础项 (可选但推荐)
        if (config.CATEGORY_ID_MAP == null || !config.CATEGORY_ID_MAP.Any() ||
            config.COMPONENT_CATEGORY_ID_MAP == null || !config.COMPONENT_CATEGORY_ID_MAP.Any())
        {
            Log("错误：配置中的类别或组件 ID 映射为空，无法进行同步。", LogLevel.Error);
            return;
        }


        _logger.LogInformation("开始同步任务...");
        string? baseUrl = null;
        try
        {
            baseUrl = await DetermineActiveUrl(config.InternalUrl, config.ExternalUrl, config.ApiKey);
        }
        catch (Exception urlEx)
        {
            Log($"尝试连接 Snipe-IT URL 时发生错误: {urlEx.Message}", LogLevel.Critical, urlEx);
            baseUrl = null; // 确保 baseUrl 为 null
        }

        if (string.IsNullOrEmpty(baseUrl))
        {
            Log("错误：内网和外网URL都无法连接或配置错误，同步任务中止。", LogLevel.Error);
            return;
        }

        try
        {
            _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Clear(); // 清除旧的 Accept 头
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // 可以设置超时时间
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // 例如 30 秒

            Log($"--- 开始同步资产到 Snipe-IT ({baseUrl}) ---");

            var mainAssetData = data.FirstOrDefault(item => item.Category == "主板/整机");
            if (mainAssetData == null)
            {
                Log("错误：未找到有效的主板/整机信息，任务中止。", LogLevel.Error);
                return;
            }

            var otherAssets = data.Where(d => config.CATEGORY_ID_MAP.ContainsKey(d.Category ?? "") && d.Category != "主板/整机").ToList();
            var components = data.Where(d => config.COMPONENT_CATEGORY_ID_MAP.ContainsKey(d.Category ?? "")).ToList();
            var accessories = data.Where(d => config.ACCESSORY_CATEGORY_ID_MAP != null && config.ACCESSORY_CATEGORY_ID_MAP.ContainsKey(d.Category ?? "")).ToList(); // 添加 null 检查

            var mainAssetId = await SyncAsset(mainAssetData, config);
            if (mainAssetId == null)
            {
                Log("错误：无法同步主资产，同步任务中止。", LogLevel.Error);
                return;
            }

            var userId = await GetOrCreateUserAsync(config);

            // 同步其他资产并签出给用户
            foreach (var asset in otherAssets)
            {
                var assetId = await SyncAsset(asset, config);
                if (assetId != null && userId != null)
                {
                    // checkoutType 应该是 "user" 因为目标是用户
                    await CheckoutAssetToUserAsync(assetId.Value, userId.Value, "user");
                }
            }

            // 同步组件并关联到主资产
            await SyncComponentsAsync(components, mainAssetId.Value, config);

            // 同步配件并签出给用户
            if (userId != null)
            {
                await SyncAccessoriesAsync(accessories, userId.Value, config);
            }

            // 最后确保主资产也被签出给用户
            if (userId != null)
            {
                await CheckoutAssetToUserAsync(mainAssetId.Value, userId.Value, "user");
            }

            // 更新主资产备注
            await UpdateAssetNotes(mainAssetId.Value, data, mainAssetData.SerialNumber);

            Log("\n--- ✅ 所有资产同步任务完成 ---");

        }
        catch (Exception ex) // 捕获整个同步过程中的未预料异常
        {
            Log($"同步过程中发生严重错误: {ex.Message}", LogLevel.Critical, ex);
            Log("--- ❌ 同步任务异常中止 ---", LogLevel.Critical);
        }
        finally
        {
            _logger.LogInformation("同步任务结束。");
            // 可以在此处清理 HttpClient 或其他资源，但通常由 DI 管理生命周期
        }
    }

    #region Private Helper Methods

    // SyncComponentsAsync (已包含日志)
    private async Task SyncComponentsAsync(List<HardwareInfo> components, int mainAssetId, SyncConfig config)
    {
        if (components == null || !components.Any())
        {
            Log("没有需要同步的组件。", LogLevel.Debug);
            return;
        }

        var componentGroups = components
            .Where(c => c != null && c.Category != null) // 过滤无效数据
            .GroupBy(c => new { Model = c.Model?.Trim(), Brand = c.Brand?.Trim(), c.Category })
            .Select(g => new
            {
                ComponentData = g.First(),
                Count = g.Count()
            });

        Log($"\n--- 正在处理 {componentGroups.Count()} 类独特的组件 ---");

        foreach (var group in componentGroups)
        {
            var componentData = group.ComponentData;
            var scannedCount = group.Count;

            // 使用传入的 config 获取 ID
            if (componentData.Category == null || !config.COMPONENT_CATEGORY_ID_MAP.TryGetValue(componentData.Category, out var categoryId))
            {
                Log($"警告：跳过组件类别 '{componentData.Category}'，未在配置中找到其 ID 映射。", LogLevel.Warning);
                continue;
            }

            var modelName = componentData.Model ?? "未知型号";
            Log($"\n--- 正在处理组件: {modelName} (扫描到数量: {scannedCount}) ---");

            var manufacturerName = componentData.Brand ?? "未知制造商";
            var manufacturer = await GetOrCreateByNameAsync(manufacturerName, "manufacturers");

            var componentPayload = new
            {
                name = modelName,
                qty = scannedCount, // 创建组件时就指定总数
                category_id = categoryId,
                manufacturer_id = manufacturer?.GetProperty("id").GetInt32()
            };

            // 查找或创建组件
            var component = await GetOrCreateByNameAsync(modelName, "components", componentPayload);
            if (component == null)
            {
                Log($"错误：无法查找或创建组件 '{modelName}'。", LogLevel.Error);
                continue;
            }

            var componentId = component.Value.GetProperty("id").GetInt32();
            Log($"  -> 正在将 {scannedCount} 个 '{modelName}' (ID: {componentId}) 组件关联(签出)到主资产 (ID: {mainAssetId})");

            // 将正确数量的组件签出（关联）到主资产
            var checkoutPayload = new { assigned_to = mainAssetId, assigned_type = "asset", assigned_qty = scannedCount };
            var checkoutResult = await ApiRequestAsync(HttpMethod.Post, $"components/{componentId}/checkout", checkoutPayload);
            if (checkoutResult == null)
            {
                Log($"  -> ❌ 关联组件 '{modelName}' 到主资产失败。", LogLevel.Error);
            }
            else
            {
                Log($"  -> ✅ 成功关联组件。");
            }
        }
    }

    // DetermineActiveUrl (已包含日志)
    private async Task<string?> DetermineActiveUrl(string? internalUrl, string? externalUrl, string apiKey)
    {
        // 优先尝试 Internal URL
        if (!string.IsNullOrWhiteSpace(internalUrl))
        {
            Log($"尝试连接内网URL: {internalUrl}...");
            try
            {
                using var tempClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) }; // 短超时
                tempClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await tempClient.GetAsync($"{internalUrl.TrimEnd('/')}/api/v1/statuslabels");
                if (response.IsSuccessStatusCode)
                {
                    Log("✅ 内网URL连接成功。");
                    return internalUrl;
                }
                else
                {
                    Log($"⚠️ 内网URL连接失败 ({response.StatusCode})。", LogLevel.Warning);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException) // 捕获网络或超时错误
            {
                Log($"⚠️ 内网URL连接失败: {ex.Message}", LogLevel.Warning);
            }
            catch (Exception ex) // 捕获其他意外错误
            {
                Log($"尝试连接内网 URL 时发生意外错误: {ex.Message}", LogLevel.Error, ex);
            }
        }
        else
        {
            Log("未配置内网 URL。", LogLevel.Debug);
        }

        // 如果 Internal URL 失败或未配置，尝试 External URL
        if (!string.IsNullOrWhiteSpace(externalUrl))
        {
            Log($"尝试连接外网URL: {externalUrl}...");
            try
            {
                using var tempClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) }; // 稍长超时
                tempClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await tempClient.GetAsync($"{externalUrl.TrimEnd('/')}/api/v1/statuslabels");
                if (response.IsSuccessStatusCode)
                {
                    Log("✅ 外网URL连接成功。");
                    return externalUrl;
                }
                else
                {
                    Log($"❌ 外网URL连接失败 ({response.StatusCode})。", LogLevel.Error);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Log($"❌ 外网URL连接失败: {ex.Message}", LogLevel.Error);
            }
            catch (Exception ex) // 捕获其他意外错误
            {
                Log($"尝试连接外网 URL 时发生意外错误: {ex.Message}", LogLevel.Error, ex);
            }
        }
        else
        {
            Log("未配置外网 URL。", LogLevel.Debug);
        }

        return null; // 如果都失败
    }

    // ApiRequestAsync (已包含日志)
    private async Task<JsonElement?> ApiRequestAsync(HttpMethod method, string endpoint, object? payload = null)
    {
        try
        {
            var request = new HttpRequestMessage(method, endpoint);
            if (payload != null)
            {
                var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                _logger.LogTrace("API Request Payload to {Endpoint}: {Payload}", endpoint, jsonPayload);
            }
            _logger.LogDebug("发送 API 请求: {Method} {Uri}", method, $"{_httpClient.BaseAddress}{endpoint}");
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API 请求失败 ({StatusCode}) to {Endpoint}. Response: {ResponseContent}", response.StatusCode, endpoint, content);
                string errorMsg = $"API 请求失败 ({response.StatusCode})";
                try
                { // 尝试解析 Snipe-IT 的错误信息
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(content);
                    if (errorJson.TryGetProperty("messages", out var messages)) errorMsg += $": {messages}";
                }
                catch { }
                Log(errorMsg, LogLevel.Error); // 报告给 UI
                return null;
            }

            _logger.LogDebug("API 请求成功 ({StatusCode}) to {Endpoint}. Response Length: {ResponseLength}", response.StatusCode, endpoint, content.Length);
            _logger.LogTrace("API Response Content: {ResponseContent}", content); // Trace 级别记录完整响应

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("API 响应体为空: {Method} {Endpoint}", method, endpoint);
                return JsonSerializer.Deserialize<JsonElement>("{}");
            }

            try { return JsonSerializer.Deserialize<JsonElement>(content); }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "解析 API 响应 JSON 时出错: {Method} {Endpoint}.", method, endpoint);
                Log($"解析 API 响应时出错: {jsonEx.Message}", LogLevel.Error);
                return null;
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "API 请求时发生网络错误: {Method} {Endpoint}", method, endpoint);
            Log($"API 请求网络错误: {httpEx.Message}", LogLevel.Error);
            return null;
        }
        catch (TaskCanceledException cancelEx) // 包括超时
        {
            _logger.LogError(cancelEx, "API 请求超时或被取消: {Method} {Endpoint}", method, endpoint);
            Log($"API 请求超时或被取消", LogLevel.Error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API 请求时发生意外错误: {Method} {Endpoint}", method, endpoint);
            Log($"API 请求异常: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    // GetOrCreateUserAsync (已包含日志)
    private async Task<int?> GetOrCreateUserAsync(SyncConfig config)
    {
        if (string.IsNullOrEmpty(config.AssignUser) || string.IsNullOrEmpty(config.AssignName))
        {
            Log("未提供用户名或姓名，跳过用户创建和资产签出。", LogLevel.Information); // 改为 Info
            return null;
        }
        Log($"\n--- 正在处理用户: {config.AssignName} ({config.AssignUser}) ---");
        var deptName = config.Department ?? "Default Department"; // 默认部门名称
        var dept = await GetOrCreateByNameAsync(deptName, "departments");
        // 如果部门创建失败，记录错误但继续尝试创建用户（不关联部门）
        int? departmentId = null;
        if (dept != null && dept.Value.TryGetProperty("id", out var deptIdProp))
        {
            departmentId = deptIdProp.GetInt32();
        }
        else
        {
            Log($"无法找到或创建部门 '{deptName}'，用户将不关联部门。", LogLevel.Warning);
        }

        var userPayload = new
        {
            username = config.AssignUser,
            first_name = config.AssignName,
            // 只有在提供了密码时才包含密码字段
            password = string.IsNullOrWhiteSpace(config.AssignPassword) ? null : config.AssignPassword,
            password_confirmation = string.IsNullOrWhiteSpace(config.AssignPassword) ? null : config.AssignPassword,
            department_id = departmentId,
            email = $"{config.AssignUser}@{config.EmailSuffix ?? "example.com"}", // 提供默认 email 后缀
            activated = true
        };
        var user = await GetOrCreateByNameAsync(config.AssignUser, "users", userPayload, "username");
        return user?.GetProperty("id").GetInt32();
    }

    // SyncAccessoriesAsync (已包含日志)
    private async Task SyncAccessoriesAsync(List<HardwareInfo> accessories, int userId, SyncConfig config)
    {
        if (accessories == null || !accessories.Any())
        {
            Log("没有需要同步的配件。", LogLevel.Debug);
            return;
        }
        Log($"\n--- 正在处理 {accessories.Count} 个配件 ---");
        foreach (var accessoryData in accessories.Where(a => a != null && a.Category != null)) // 过滤无效数据
        {
            if (!config.ACCESSORY_CATEGORY_ID_MAP.TryGetValue(accessoryData.Category ?? "", out var categoryId))
            {
                Log($"警告：跳过配件类别 '{accessoryData.Category}'，未在配置中找到其 ID 映射。", LogLevel.Warning);
                continue;
            }

            var modelName = accessoryData.Model ?? "未知型号";
            Log($"\n--- 正在处理配件: {modelName} ---");

            var manufacturerName = accessoryData.Brand ?? "未知制造商";
            var manufacturer = await GetOrCreateByNameAsync(manufacturerName, "manufacturers");

            var accessoryPayload = new
            {
                name = modelName,
                category_id = categoryId,
                manufacturer_id = manufacturer?.GetProperty("id").GetInt32(),
                qty = 1 // 假设每个扫描到的配件代表一个物理单位
            };
            // 查找或创建配件
            var accessory = await GetOrCreateByNameAsync(modelName, "accessories", accessoryPayload);
            if (accessory == null)
            {
                Log($"错误：无法查找或创建配件 '{modelName}'。", LogLevel.Error);
                continue;
            }

            var accessoryId = accessory.Value.GetProperty("id").GetInt32();
            // 配件签出给用户
            await CheckoutAssetToUserAsync(accessoryId, userId, "accessory");
        }
    }

    // CheckoutAssetToUserAsync (已包含日志)
    private async Task CheckoutAssetToUserAsync(int assetOrAccessoryId, int userId, string checkoutType)
    {
        var endpoint = (checkoutType == "user") ? "hardware" : "accessories";
        var assetTypeName = (checkoutType == "accessory") ? "配件" : "资产";

        Log($"  -> 正在将 {assetTypeName} (ID: {assetOrAccessoryId}) 签出给用户 (ID: {userId})");

        var details = await ApiRequestAsync(HttpMethod.Get, $"{endpoint}/{assetOrAccessoryId}");
        if (details?.TryGetProperty("assigned_to", out var assignedTo) == true && assignedTo.ValueKind != JsonValueKind.Null)
        {
            string? assignedToType = null;
            if (assignedTo.TryGetProperty("type", out var typeProp)) assignedToType = typeProp.GetString();

            if (assignedToType == "user" && assignedTo.TryGetProperty("id", out var assignedId) && assignedId.GetInt32() == userId)
            {
                Log($"  -> ℹ️ {assetTypeName} (ID: {assetOrAccessoryId}) 已签出给此用户 (ID: {userId})，跳过操作。");
                return;
            }
            else
            {
                Log($"  -> ℹ️ {assetTypeName} (ID: {assetOrAccessoryId}) 已被签出给 {assignedToType} (ID: {assignedTo.GetProperty("id")})。将尝试覆盖签出...");
            }
        }

        var checkoutPayload = new { checkout_to_type = "user", assigned_user = userId };

        var response = await ApiRequestAsync(HttpMethod.Post, $"{endpoint}/{assetOrAccessoryId}/checkout", checkoutPayload);

        if (response?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
        {
            Log($"    - ✅ 成功将 {assetTypeName} (ID: {assetOrAccessoryId}) 签出给用户 (ID: {userId})。");
        }
        else
        {
            var messages = response?.TryGetProperty("messages", out var msg) == true ? msg.ToString() : "未知错误";
            Log($"    - ❌ 将 {assetTypeName} (ID: {assetOrAccessoryId}) 签出给用户 (ID: {userId}) 失败。原因: {messages}", LogLevel.Error); // 改为 Error
        }
    }

    // UpdateAssetNotes (已包含日志)
    private async Task UpdateAssetNotes(int assetId, List<HardwareInfo> allData, string? mainSerial)
    {
        Log("\n--- 正在生成并更新主资产备注信息 ---");
        var notesBuilder = new StringBuilder("\n--- 自动同步的硬件信息 ---\n");
        foreach (var item in allData.Where(i => i != null).OrderBy(i => i.Category)) // 添加排序和 null 检查
        {
            var line = $"[{item.Category}] {item.Brand} {item.Model}".Trim();
            if (!string.IsNullOrEmpty(item.Size) && item.Size != "N/A") line += $" ({item.Size})";
            // 只在备注中添加非主资产的序列号
            if (!string.IsNullOrEmpty(item.SerialNumber) && item.SerialNumber != "N/A" && item.SerialNumber != "无法获取" && item.SerialNumber != mainSerial)
            {
                line += $" - SN: {item.SerialNumber}";
            }
            notesBuilder.AppendLine(line);
        }
        notesBuilder.AppendLine($"\n--- 同步时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---"); // 添加时间戳

        var payload = new { notes = notesBuilder.ToString() };
        var updateResult = await ApiRequestAsync(new HttpMethod("PATCH"), $"hardware/{assetId}", payload);
        if (updateResult != null)
        {
            Log("  -> ✅ 备注信息更新成功。");
        }
        else
        {
            Log("  -> ❌ 备注信息更新失败。", LogLevel.Warning); // 改为 Warning
        }
    }

    // GetOrCreateByNameAsync (已包含日志)
    private async Task<JsonElement?> GetOrCreateByNameAsync(string name, string endpoint, object? creationPayload = null, string searchField = "name")
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            Log($"跳过无效的名称查找/创建: '{name}' in {endpoint}", LogLevel.Debug);
            return null;
        }
        var entityType = endpoint.TrimEnd('s');
        Log($"  -> 正在按名称查询 {entityType}: {name}...");

        JsonElement? searchResult = null;
        try { searchResult = await ApiRequestAsync(HttpMethod.Get, $"{endpoint}?search={Uri.EscapeDataString(name)}"); }
        catch (Exception ex) { Log($"查询 {endpoint} 时出错: {ex.Message}", LogLevel.Error, ex); return null; }


        if (searchResult?.TryGetProperty("total", out var total) == true && total.GetInt32() > 0)
        {
            foreach (var item in searchResult.Value.GetProperty("rows").EnumerateArray())
            {
                // 进行不区分大小写的比较
                if (item.TryGetProperty(searchField, out var fieldValue) && fieldValue.GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var foundId = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1;
                    Log($"  -> ✅ 已找到 (ID: {foundId})");
                    return item;
                }
            }
            Log($"  -> 未找到完全匹配项，将创建新的 {entityType}。"); // 即使 total > 0，也可能没有精确匹配
        }
        else
        {
            Log($"  -> 不存在，正在创建新的 {entityType}...");
        }


        // 创建新实体
        var payload = creationPayload ?? new { name };
        JsonElement? createResult = null;
        try { createResult = await ApiRequestAsync(HttpMethod.Post, endpoint, payload); }
        catch (Exception ex) { Log($"创建 {endpoint} 时出错: {ex.Message}", LogLevel.Error, ex); return null; }


        if (createResult?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
        {
            // 尝试从 payload 或直接从结果中获取新 ID
            int? newId = null;
            JsonElement? newEntity = null;
            if (createResult.Value.TryGetProperty("payload", out var newPayload) && newPayload.ValueKind == JsonValueKind.Object)
            {
                if (newPayload.TryGetProperty("id", out var idProp)) newId = idProp.GetInt32();
                newEntity = newPayload;
            }
            else if (createResult.Value.TryGetProperty("id", out var idPropDirect)) // 有些 API 直接返回对象
            {
                newId = idPropDirect.GetInt32();
                newEntity = createResult.Value;
            }

            if (newId.HasValue)
            {
                Log($"  -> ✅ 成功创建 (新 ID: {newId.Value})");
                return newEntity; // 返回新创建的对象/payload
            }
            else
            {
                Log($"  -> ⚠️ 创建成功，但无法从响应中提取新 ID。", LogLevel.Warning);
                return createResult; // 返回原始成功响应
            }
        }
        else // 创建失败
        {
            var messages = createResult?.TryGetProperty("messages", out var msg) == true ? msg.ToString() : "未知错误";
            Log($"  -> ❌ 创建 {entityType} '{name}' 失败: {messages}", LogLevel.Error);
            return null;
        }
    }

    // GetNextAssetTagAsync (已包含日志)
    private async Task<string?> GetNextAssetTagAsync(string prefix)
    {
        Log($"  -> 正在为前缀 '{prefix}' 查询下一个可用资产标签...");
        JsonElement? searchResult = null;
        try { searchResult = await ApiRequestAsync(HttpMethod.Get, $"hardware?search={prefix}&sort=asset_tag&order=desc&limit=1"); }
        catch (Exception ex) { Log($"查询资产标签时出错: {ex.Message}", LogLevel.Error, ex); return null; } // 返回 null 表示失败

        string nextTag = $"{prefix}0001"; // 默认起始标签

        if (searchResult?.TryGetProperty("total", out var total) == true && total.GetInt32() > 0)
        {
            var rows = searchResult.Value.GetProperty("rows");
            if (rows.GetArrayLength() > 0)
            {
                var lastTag = rows[0].TryGetProperty("asset_tag", out var tagProp) ? tagProp.GetString() : null;
                if (!string.IsNullOrEmpty(lastTag))
                {
                    _logger.LogDebug("找到最后一个匹配前缀的标签: {LastTag}", lastTag);
                    // 尝试提取标签末尾的数字部分
                    var match = Regex.Match(lastTag, @"(\d+)$");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
                    {
                        nextTag = $"{prefix}{(num + 1):D4}"; // 生成下一个标签，补零
                    }
                    else
                    {
                        Log($"无法从最后一个标签 '{lastTag}' 中解析数字后缀，将使用默认起始标签。", LogLevel.Warning);
                    }
                }
                else
                {
                    Log("最后一个匹配的资产标签为空，将使用默认起始标签。", LogLevel.Warning);
                }
            }
            else
            {
                Log("查询返回 total > 0 但 rows 为空，将使用默认起始标签。", LogLevel.Warning);
            }
        }
        else
        {
            Log($"未找到前缀为 '{prefix}' 的资产，将使用默认起始标签 {nextTag}。");
        }

        Log($"  -> ✅ 下一个可用资产标签是: {nextTag}");
        return nextTag;
    }

    // SyncAsset (已包含日志)
    private async Task<int?> SyncAsset(HardwareInfo assetData, SyncConfig config)
    {
        if (assetData == null) { Log("资产数据为 null，跳过。", LogLevel.Warning); return null; }

        var serial = assetData.SerialNumber;
        if (string.IsNullOrEmpty(serial) || serial.Equals("N/A", StringComparison.OrdinalIgnoreCase) || serial.Equals("无法获取", StringComparison.OrdinalIgnoreCase))
        {
            Log($"跳过资产 '{assetData.Model}'，因为它没有有效的序列号。", LogLevel.Warning);
            return null;
        }
        Log($"\n--- 正在处理独立资产, 序列号: {serial} (类别: {assetData.Category}) ---");

        var manufacturerName = assetData.Brand ?? "未知制造商";
        var manufacturer = await GetOrCreateByNameAsync(manufacturerName, "manufacturers");
        if (manufacturer == null)
        {
            Log($"无法找到或创建制造商 '{manufacturerName}'，跳过资产 '{assetData.Model}'。", LogLevel.Error);
            return null;
        }

        // 确定类别名称和 ID
        var categoryName = assetData.Category == "主板/整机" ? "台式机" : assetData.Category; // 默认将主板/整机映射为台式机
        if (categoryName == null || !config.CATEGORY_ID_MAP.TryGetValue(categoryName, out var categoryId))
        {
            Log($"错误：未在配置中为资产类别 '{categoryName ?? "NULL"}' 配置有效的 ID 映射。", LogLevel.Error);
            return null;
        }

        var modelName = assetData.Model ?? "未知型号";
        var modelPayload = new { name = modelName, category_id = categoryId, manufacturer_id = manufacturer.Value.GetProperty("id").GetInt32() };
        var model = await GetOrCreateByNameAsync(modelName, "models", modelPayload);
        if (model == null)
        {
            Log($"无法找到或创建型号 '{modelName}'，跳过资产。", LogLevel.Error);
            return null;
        }

        // 检查资产是否已存在 (通过序列号)
        Log($"  -> 正在通过序列号 '{serial}' 查找现有资产...");
        var existingAsset = await ApiRequestAsync(HttpMethod.Get, $"hardware/byserial/{Uri.EscapeDataString(serial)}");
        if (existingAsset?.TryGetProperty("total", out var total) == true && total.GetInt32() > 0)
        {
            var rows = existingAsset.Value.GetProperty("rows");
            if (rows.GetArrayLength() > 0)
            {
                var existingAssetId = rows[0].GetProperty("id").GetInt32();
                var existingAssetTag = rows[0].GetProperty("asset_tag").GetString();
                Log($"  -> ✅ 资产已存在 (ID: {existingAssetId}, Tag: {existingAssetTag})。");
                // 可选：在这里可以添加更新逻辑，例如更新型号、名称或备注
                return existingAssetId;
            }
        }

        // 创建新资产
        Log($"  -> 资产不存在，正在创建...");
        var categoryCode = config.CATEGORY_CODE_MAP.GetValueOrDefault(categoryName, "AST"); // 默认代码 AST
        var tagPrefix = $"{config.ASSET_TAG_PREFIX}-{categoryCode}-";

        var nextTag = await GetNextAssetTagAsync(tagPrefix);
        if (nextTag == null)
        {
            Log("无法生成下一个资产标签，创建资产失败。", LogLevel.Error);
            return null; // 如果无法生成标签，则无法创建
        }

        // 确定资产名称，可以包含机器名或其他信息
        var assetName = $"{categoryName} - SN:{serial}"; // 使用更具体的名称
        try { assetName = $"{categoryName} - {Environment.MachineName}"; } catch { } // 尝试获取机器名，失败则忽略

        var assetPayload = new
        {
            model_id = model.Value.GetProperty("id").GetInt32(),
            serial = serial,
            name = assetName,
            status_id = 2, // 状态 ID 2 通常是 "Ready to Deploy"
            asset_tag = nextTag
        };

        var createResult = await ApiRequestAsync(HttpMethod.Post, "hardware", assetPayload);
        if (createResult?.TryGetProperty("status", out var status) == true && status.GetString() == "success")
        {
            if (createResult.Value.TryGetProperty("payload", out var payloadElement) && payloadElement.TryGetProperty("id", out var idElement))
            {
                var newAssetId = idElement.GetInt32();
                Log($"  -> ✅ 成功创建资产 (新 ID: {newAssetId}, Tag: {nextTag})");
                return newAssetId;
            }
            else
            {
                Log("  -> ⚠️ 创建资产成功，但无法从响应中提取新 ID。", LogLevel.Warning);
                return null; // 或者尝试再次查询获取 ID
            }
        }
        else
        {
            var messages = createResult?.TryGetProperty("messages", out var msg) == true ? msg.ToString() : "未知错误";
            Log($"  -> ❌ 创建资产失败: {messages}", LogLevel.Error);
            return null;
        }
    }
    #endregion
}