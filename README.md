# JoSystem 文件服务器基础框架（WPF + Hosting + .NET Core + HTML）

JoSystem 是一个将 WPF 桌面应用与 ASP.NET Core Kestrel Web 服务融合在同一进程内的基础框架，提供托管宿主、配置管理、日志审计与服务化运行等通用能力，可在其上按需叠加文件管理等业务模块，适合作为桌面工具 + 轻量级内置 Web 服务的标准模板。

## 技术栈与特性

- WPF 桌面界面（MVVM）
- ASP.NET Core Kestrel 自托管 Web 服务（MVC 控制器 + 部分最小 API）
- EF Core + SQLite 配置与用户管理
- 多数据库连接管理（Oracle/SQL Server）与 SQL 在线调试
- WebSocket 实时状态通知
- 嵌入式 HTML 前端（打包为 EmbeddedResource）
- Windows 服务模式运行与托盘集成

## 项目架构设计

- 进程模型：同一进程同时承载 WPF UI 与 Kestrel WebServer，WPF 通过 ViewModel 控制 WebServer 生命周期
- 模块划分：
  - 核心入口与模式切换：[Program.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Program.cs)、[App.xaml.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/App.xaml.cs)
- Web 服务与路由：[WebServer.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Hosting/WebServer.cs)、[ApiController.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Hosting/ApiController.cs)
- 配置与数据：[ConfigService.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Infrastructure/ConfigService.cs)、[AppDbContext.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Data/AppDbContext.cs)
- 桌面端 MVVM：[HomeViewModel.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/ViewModels/HomeViewModel.cs) 及 Views
  - 前端页面（嵌入式）：[index.html](file:///d:/Source/BackEnd/JoSystem/JoSystem/WebPages/index.html)
- 数据流与交互（基础框架）：
  - UI 操作 → ViewModel 启停 WebServer → WebServer 通过 WebSocket 向前端广播 serverStatus/heartbeat
  - 前端调用 Web API 控制器完成登录鉴权与日志查询；文件相关 API 作为可选业务模块提供

## 各技术组件集成原理

- Kestrel 自托管：在 WebServer 中通过 Host.CreateDefaultBuilder 配置 Kestrel 监听端口、启用 WebSocket、映射 Web API 控制器与业务模块路由，并将 index.html 作为默认响应，同时集成 Swagger 文档（默认地址 `/mapa/swagger`）
- 配置层级：代码默认值 → 本地配置文件（可选）→ SQLite 数据库持久化（界面修改即时写入并覆盖应用）
- 认证机制：受保护的 API 路径前缀由 ConfigService 控制，Cookie（IsLoggedIn/Username）用作轻量登录态
- 前端嵌入：csproj 将 WebPages/**/* 标记为 EmbeddedResource，运行时从程序集资源流返回 index.html

## 开发环境配置步骤

- 安装 .NET SDK 9.0（Windows Desktop）
- 安装 Visual Studio 2022（启用“使用 .NET 进行桌面开发”工作负载）
- 推荐工具：PowerShell、Git、SQLite 查看器
- 依赖：
  - Microsoft.AspNetCore.App（FrameworkReference）
  - Microsoft.EntityFrameworkCore.Sqlite
  - System.ServiceProcess.ServiceController
  - Swashbuckle.AspNetCore（Swagger 文档/界面）
  - 详见 [JoSystem.csproj](file:///d:/Source/BackEnd/JoSystem/JoSystem/JoSystem.csproj)

## 项目启动与调试流程

- 桌面模式：
  - Visual Studio 打开解决方案 [JoSystem.sln](file:///d:/Source/BackEnd/JoSystem/JoSystem/JoSystem.sln)
  - 选择启动项目 JoSystem，F5 运行
  - 在“文件服务器”视图点击“启动服务器”，浏览器将自动打开主页
- 服务模式：
  - 管理员运行，进入“系统设置”切换“以服务模式运行”，保存
  - 首次将安装 Windows 服务（JoSystemService），随后可在设置中启动/停止
  - 适用于无人值守后台运行

## 关键代码结构解析

- 入口与模式切换：桌面/服务双模式判定与运行逻辑见 [Program.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Program.cs)
- 应用启动：加载数据库与配置、设置语言、展示主窗体见 [App.xaml.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/App.xaml.cs)
- WebServer 组装：
  - 端口占用检测、HTTP/HTTPS 双监听与证书获取见 [WebServer.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Hosting/WebServer.cs)
  - WebSocket 管理与心跳广播见 [WebSocketManager.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Hosting/WebSocketManager.cs)
  - 路由、控制器映射与 Swagger 配置见 [WebServer.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Hosting/WebServer.cs) 与控制器目录 [Controllers](file:///d:/Source/BackEnd/JoSystem/JoSystem/Controllers)
- 配置与持久化：
  - 配置模型、默认值与持久化加载见 [ConfigService.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/ConfigService.cs)
  - EF Core SQLite 上下文与关系见 [AppDbContext.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Data/AppDbContext.cs)
- 桌面端控制：
-  - 启停服务器、端口检查、URL 生成与自动打开浏览器见 [HomeViewModel.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/ViewModels/HomeViewModel.cs#L418-L480)
- 前端交互：
  - WebSocket 连接与状态展示、登录弹窗与 Cookie 同步见 [index.html](file:///d:/Source/BackEnd/JoSystem/JoSystem/WebPages/index.html#L383-L431)

## 依赖注入与模块化配置示例

- ASP.NET Core 服务注册（如需扩展 DI）：

```csharp
// 在 WebServer.cs 的 ConfigureWebHostDefaults 中添加：
webBuilder.ConfigureServices(services =>
{
    services.AddSingleton<IMediaPreviewService, MediaPreviewService>();
    // services.AddLogging(); // 可按需扩展
});
```

- 受保护 API 与黑白名单配置：
  - 通过“系统设置”界面修改并持久化到 SQLite，示例键：ProtectedApiPaths、BlacklistExtensions、UnCachedViews
  - 前端预览类型映射由 PreviewFileTypes 控制（扩展常见类型）

## Swagger 接口文档与安全访问控制

- 启用开关
  - 配置项：`EnableSwagger`（布尔），在“系统设置”列表中可修改
  - 为 `True` 且请求 IP 命中白名单时，可访问 Swagger 文档；为 `False` 时完全关闭

- 访问地址
  - 默认地址：`http://{host}:{HttpPort}/mapa/swagger`
  - 文档 JSON：`http://{host}:{HttpPort}/mapa/swagger/v1/swagger.json`

- IP 白名单配置（`SwaggerIpWhitelist`）
  - 通过“系统设置”界面维护，支持多条规则，使用 `;`、`,`、`|` 或空格分隔
  - 默认值：`0.0.0.0/0;::/0`（允许所有 IPv4/IPv6 地址，生产环境建议自行收紧）
  - 支持格式：
    - 单 IP：`10.1.15.34`
    - 网段（CIDR）：`192.168.0.0/24`、`10.0.0.0/8`
    - IP 区间：`10.1.0.1-10.2.0.23`
    - 通配符（仅 IPv4）：`10.1.*.5`
  - 示例：
    - 允许全网访问：`0.0.0.0/0;::/0`
    - 允许一个网段和两个单 IP：`10.1.0.1-10.2.0.23;10.1.15.34;10.1.15.77`
    - 允许某模式 IP：`10.1.*.5`

- 使用提示（Settings 界面中的 Tip）
  - 每个配置项的“说明”列文本来自多语言资源（`Lang.Config.*`），悬停在“值”列单元格上会显示同样内容作为提示
  - Swagger 相关配置的说明中已包含完整语法示例，方便直接在界面中查看用法
  - 业务 API 如需出现在 Swagger 中，推荐以 MVC 控制器形式实现（如 `Controllers/Modules/*`），仅内部使用且不需要文档的接口可继续通过 `IAppModule.MapApis` 注册最小 API

## HTML 与 WPF 交互机制说明

- 事件流：WPF 启动/停止 WebServer → WebServer 通过 WebSocket 广播 serverStatus/heartbeat → 前端更新状态与最近心跳时间
- 前端 API 调用：登录、鉴权、日志查询均通过 fetch 调用 Web API 控制器
- 文件浏览 / 下载等能力由示例模块提供，不属于框架必选部分

## 性能优化建议

- 流式 I/O：上传采用分段写入与原子落盘；下载支持 Range 与 64KB 缓冲
- 限流与阈值：Kestrel MaxRequestBodySize、MaxUploadSizeGB/MaxDownloadSizeGB 持久化管控
- 目录监控：WPF 端文件监听防抖（DispatcherTimer）避免频繁刷新
- WebSocket：心跳与状态合并推送，前端断线重连并延迟显示“断开”提升体验

## 常见问题解决方案

- 端口占用：修改设置中的 HttpPort/HttpsPort，或释放冲突进程
- 权限错误（服务模式）：以管理员运行或使用 UAC 提权安装/启动服务
- 403 未授权：受保护路径需登录，检查 Cookie（IsLoggedIn/Username）
- HTTPS 证书：首次自动生成或复用现有证书失败时，检查系统证书存储/权限

## 数据库多连接管理与 SQL 调试

- **多数据库配置**：
  - 在“数据库连接”导航页中管理多个数据库连接配置（Oracle / SQL Server）
  - 支持连接的新增、编辑、删除、排序及启用/禁用
  - 自动保存配置到 SQLite 数据库，重启后自动加载
  - 提供“测试连接”功能验证连接字符串有效性

- **SQL 在线调试**：
  - 内置 SQL 查询调试工具，支持选中特定的数据库连接执行查询
  - 安全限制：仅允许执行 `SELECT` 语句，防止数据意外修改，最大返回 200 行
  - 交互优化：当前选中的数据库名称红字高亮提示，查询结果框固定高度并支持内部滚动
  - 结果展示：查询结果以格式化的 JSON 呈现，支持语法高亮与长内容滚动查看

- **用户管理增强**：
  - 提供用户密码修改功能，支持管理员在界面直接重置用户密码
  - 优化表格交互，支持行选中与操作反馈，提升用户体验

## 扩展功能开发指南

- 新增 API：
  - 推荐：在 Controllers 目录中新增控制器（带 `[ApiController]` 和路由特性），例如：
    - 纯平台接口：放在 `Controllers` 根命名空间下（如 `AuthController`、`LogController`）
    - 业务模块接口：放在 `Controllers/Modules/{ModuleName}` 下（如 `Modules.Samples.SampleController`）
  - 若仅用于内部扩展且不需要出现在 Swagger 文档中，可在内部使用最小 API（`MapGet/MapPost`）注册路由
- 扩展前端：
  - 在 [index.html](file:///d:/Source/BackEnd/JoSystem/JoSystem/WebPages/index.html) 增加视图或与 WebSocket 新事件交互，重新编译即生效
- 模块化视图：
  - 通过 ConfigService 的 ViewRegistry 与 UnCachedViews 控制视图缓存与导航

## 完整的 API 接口文档与使用示例

- 认证
  - POST /api/login（由 `AuthController` 提供）
    - 请求体：`{ "Username": "admin", "Password": "123456" }`
    - 响应：`{ "success": true, "message": "登录成功" }`
  - POST /api/logout
  - GET /api/auth → `{ authenticated: boolean, username?: string }`
- 日志
  - GET /api/logs?keyword=&level=&source=&startTime=&endTime=&pageIndex=&pageSize=
- 特殊路径
  - GET /api/update/{**name} → 映射到 Root/api/update/...

```bash
# 登录
curl -X POST http://localhost:5000/api/login \
  -H "Content-Type: application/json" \
  -d "{\"Username\":\"admin\",\"Password\":\"123456\"}" -i
```

## 日志系统说明

- 按日滚动：log_yyyyMMdd.log
- 级别过滤：MinLogLevel（0=Info，1=Warning，2=Error）
- 查询优化：文件预过滤 + 流式读取 + 分页

## 配置项与优先级

- 优先级：数据库（界面修改）＞ 配置文件（可选）＞ 代码默认值
- 关键项：RootDirectory、LogDirectory、MaxUploadSizeGB、MaxDownloadSizeGB、LogPageSize、EnableHttps、HttpPort/HttpsPort、PreviewFileTypes、BlacklistExtensions、ProtectedApiPaths、RunAsService

## 项目截图与运行效果演示

- 建议将运行截图放置 `Docs/screenshots/` 并在此引用：
  - ![主界面](Docs/screenshots/main.png)
  - ![文件浏览](Docs/screenshots/files.png)
  - ![日志视图](Docs/screenshots/logs.png)
  - ![前端页面](Docs/screenshots/web.png)

## 参考文件与链接

- 解决方案与工程：[JoSystem.sln](file:///d:/Source/BackEnd/JoSystem/JoSystem/JoSystem.sln)、[JoSystem.csproj](file:///d:/Source/BackEnd/JoSystem/JoSystem/JoSystem.csproj)
- 核心服务：[WebServer.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Services/Hosting/WebServer.cs)、控制器目录 [Controllers](file:///d:/Source/BackEnd/JoSystem/JoSystem/Controllers)
- 数据访问：[AppDbContext.cs](file:///d:/Source/BackEnd/JoSystem/JoSystem/Data/AppDbContext.cs)
- 前端资源：[index.html](file:///d:/Source/BackEnd/JoSystem/JoSystem/WebPages/index.html)
