<div align="center">

&nbsp; <h1>Asset Sonar (资产声呐)</h1>

&nbsp; <p> <strong>一款为 IT 专业人员和系统管理员量身打造的现代化、高效率 Windows 桌面工具，旨在将硬件资产的扫描、发现、导出和同步流程化繁为简。</strong> &nbsp; </p>

&nbsp; <p> <a href="README.md"><img src="https://img.shields.io/badge/language-zh--CN-green.svg" alt="语言"></a> <img src="https://img.shields.io/badge/.NET-8.0-blueviolet" alt=".NET 8.0"> <img src="https://img.shields.io/badge/Platform-Windows%2010%2B-blue" alt="平台"> <a href="LICENSE.txt"><img src="https://img.shields.io/badge/License-GPLv3-blue.svg" alt="许可证"></a> &nbsp; </p>

</div>

Asset Sonar 是一款基于 WinUI 3 和 .NET 8 构建的强大应用程序。它拥有一个直观且现代的用户界面，能够像声呐一样精准、深度地扫描本地计算机的硬件配置和局域网内的在线设备，并将这些宝贵的数据无缝同步到 Snipe-IT 资产管理平台，或一键导出为多种格式的专业报告。

✨ 主要功能
-----------

💻 **全方位硬件扫描**: 深入扫描并聚合关键硬件信息，覆盖范围广泛：
* **主板/整机**: 品牌、型号、序列号，并为戴尔、惠普、联想等主流品牌自动生成保修查询链接。
* **处理器 (CPU)**: 制造商、完整型号及处理器ID。
* **内存 (RAM)**: 制造商、型号、容量、序列号、类型 (DDR, DDR2, DDR3, DDR4, DDR5) 和频率。
* **显卡 (GPU)**: 显卡型号。
* **硬盘 (Disk)**: 型号、容量和序列号。
* **显示器**: 制造商、型号和序列号。
* **网络适配器**: 物理网卡的型号和MAC地址。
* **外设**: 识别键盘和鼠标。
* **系统信息**: 操作系统版本、序列号及激活状态。
* **IP 地址**: 活动网卡的 IP 地址、子网掩码和默认网关。

🌐 **局域网设备发现**:
* 扫描并发现局域网内的所有活动设备，以卡片形式直观展示其 IP 地址、主机名、在线状态和网络延迟(ms)。
* 结果按 IP 地址排序显示。

🔄 **无缝 Snipe-IT 集成**:
* 通过 API 将扫描到的硬件信息 **自动同步** 到您的 Snipe-IT 平台。
* **智能资产管理**：根据序列号自动判断是创建新资产还是更新现有资产。
* **自动关联**：自动创建或关联制造商、型号、分类，并将内存、硬盘等作为组件关联到主资产。
* **用户分配**：可配置将扫描到的资产（包括主资产、显示器等）和配件（键盘、鼠标）自动分配（Checkout）给指定用户。
* **UI化配置**：通过应用内的“配置”页面管理 Snipe-IT 服务器信息、用户信息以及 **类别ID映射** 和 **资产标签前缀/代码**。
* **多配置方案支持**：通过便携的 `profiles.json` 文件在应用内管理多套 Snipe-IT 服务器配置和用户信息。

📄 **多格式数据导出**: 一键将扫描到的硬件信息导出为多种专业报告格式：
* **Excel**: 生成结构化的 `.xlsx` 工作簿，包含可点击的保修超链接。
* **CSV**: 快速生成通用的 `.csv` 文件（UTF-8编码），兼容各类数据处理工具。
* **PDF**: 创建专业、美观的 `.pdf` 硬件信息报告，方便存档和分享。

🩺 **系统健康诊断**:
* 运行一系列健康检查，快速评估系统状态，包括 CPU/内存使用率、关键服务状态、硬盘健康度 (S.M.A.R.T.) 以及内外网连通性。

🎨 **现代化的用户界面**:
* 基于 WinUI 3 构建，拥有流畅的动画、云母（Mica）/亚克力（Acrylic）背景特效和现代设计风格，提供卓越的用户体验。
* 清晰的导航栏，可在主页、网络扫描、系统诊断、配置和 关于 页面之间轻松切换。

📸 应用截图
-------------

*（建议在此处添加应用截图，展示主界面、网络扫描、配置页面等）*

主界面: 清晰展示扫描模块、操作按钮、日志输出和详细硬件列表。
网络扫描: 以卡片形式直观显示局域网中的在线设备。
配置页面: 图形化管理 Snipe-IT 连接信息和 ID 映射。
关于页面: 展示应用、制作人及开源协议信息。

🛠️ 技术栈与架构
----------------

Asset Sonar 采用了一系列现代 .NET 技术和成熟的软件设计模式，确保了其高性能、高可扩展性和高可维护性。

* **框架**: .NET 8, Windows App SDK (WinUI 3)
* **架构模式**:
    * **MVVM (Model-View-ViewModel)**: 使用 CommunityToolkit.Mvvm 库实现，彻底分离了UI和业务逻辑。
    * **依赖注入 (Dependency Injection)**: 通过 Microsoft.Extensions.DependencyInjection 统一管理各个模块的生命周期和依赖关系，降低了代码耦合度，提升了可测试性。
    * **插件化架构**: 核心功能（扫描、导出、同步、诊断）都被实现为独立的插件，通过 `PluginManager` 在运行时动态加载 `ItAssetTool.Plugins.dll`。 这使得添加新功能或替换现有实现变得异常简单。
* **核心库**:
    * `System.Management`: 用于通过 WMI (Windows Management Instrumentation) 获取底层硬件信息。
    * `ClosedXML`: 用于创建和操作 Excel (.xlsx) 文件。
    * `QuestPDF`: 用于生成专业、美观的 PDF 文档。

🚀 开始使用
-----------

1.  **克隆仓库**:
    ```bash
    git clone [https://github.com/axuan9945/asset-sonar.git](https://github.com/axuan9945/asset-sonar.git)
    cd asset-sonar
    ```
   
2.  **打开解决方案**: 使用 Visual Studio 2022 (或更高版本) 打开 `ItAssetTool.sln` 文件。请确保已安装 **.NET 桌面开发** 和 **通用 Windows 平台开发** 工作负载。
3.  **恢复依赖**: Visual Studio 应该会自动恢复所有 NuGet 包。
4.  **运行项目**: 在 Visual Studio 的顶部工具栏中，选择 `ItAssetTool (Unpackaged)` 作为启动项目，然后点击 "运行" 按钮 (或按 F5)。

⚙️ 配置
-------

首次运行后，应用会在 **程序运行的根目录** (与 `.exe` 文件同级) 自动创建一个 `profiles.json` 文件。您可以通过应用内的 **“配置”** 页面来图形化地管理 Snipe-IT 的连接信息和同步设置：

* **配置方案**: 支持保存多套配置，方便在不同服务器环境间切换。
* **服务器 URL**: 内网和外网的 Snipe-IT 服务器地址。
* **API Key**: 您的 Snipe-IT API 密钥。
* **资产标签前缀**: 用于生成新资产标签的前缀。
* **用户分配信息**: 用于将扫描到的资产自动分配给指定的用户和部门。
* **ID 和代码映射**: 配置 Snipe-IT 中资产类别、组件类别、配件类别对应的 ID，以及资产类别对应的代码（用于生成资产标签）。

此外，项目根目录还包含一个 `snipeit_config.json` 文件，目前主要用于定义扫描时需要 **忽略的设备关键字** (如虚拟网卡)。

🤝 贡献
-------

我们热烈欢迎任何形式的贡献！无论是提交 Bug 报告、提出功能建议还是直接贡献代码，都将使这个项目变得更好。

1.  Fork 本仓库
2.  创建您的功能分支 (`git checkout -b feature/AmazingFeature`)
3.  提交您的更改 (`git commit -m 'feat: Add some AmazingFeature'`)
4.  推送到分支 (`git push origin feature/AmazingFeature`)
5.  提交一个 Pull Request

📄 许可证
---------

本项目采用 **GNU General Public License v3.0** 许可证。详情请参阅 [LICENSE.txt](LICENSE.txt) 文件。

🙏 致谢
-------

* **CommunityToolkit.Mvvm**: 极大地简化了 MVVM 模式的实现。
* **ClosedXML**: 提供了强大而易用的 Excel 文件操作功能。
* **QuestPDF**: 一个出色、简洁的 .NET PDF 生成库。
* 所有为 .NET 和 Windows App SDK 生态系统做出贡献的开发者们。