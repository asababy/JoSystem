# JoSystem 模块化架构说明（平台层 + 业务模块层）

本说明文档用于团队内部理解 JoSystem 的“基础平台 + 业务模块”架构，以及如何按客户（CustomerA / CustomerB）拆分项目与交付代码。

---

## 1. 整体分层

- 平台层（Platform / JoSystem）
  - 当前 JoSystem 项目本身
  - 提供：WPF Shell、Hosting（Kestrel）、Windows 服务、配置管理、日志、认证、数据库访问、嵌入式前端等通用基础能力
  - 通过 Controllers + Services + Repository 模式对外提供 Web API
  - 源码仅在公司内部维护，对客户只以 DLL / EXE 形式交付

- 业务模块层（Business Modules）
  - 按客户或业务域拆分，例如：CustomerA.Modules、CustomerB.Modules
  - 在各自项目中实现业务领域的 Controllers / Services / Repository
  - 可选：为每个模块提供对应的 WPF View / ViewModel，用于集成到 Shell 导航中
  - 业务模块源码可以交付给客户，作为定制部分

---

## 2. 平台层关键角色

- WebServer（JoSystem.Services.Hosting.WebServer）
  - 承载 ASP.NET Core Kestrel
  - 配置 HTTP/HTTPS 监听、WebSocket、认证中间件
  - 在 `app.UseEndpoints` 中：
    - 映射平台内置与业务 Web API 控制器：`e.MapControllers()`
    - 遍历模块注册表 `AppModuleRegistry.Modules`，调用每个模块的 `MapApis` 进行扩展（可选最小 API，不强制出现在 Swagger）

- ApiController（JoSystem.Services.Hosting.ApiController）
  - 作为平台早期最小 API 的兼容封装，目前仅保留扩展点（MapFileServerApis）
  - 平台内置接口（登录 / 登出 / 鉴权 / 日志查询等）已经迁移到 MVC 控制器（例如 `AuthController`、`LogController`）

- ConfigService / LogService / DbService
  - 提供统一的配置、日志、数据库访问能力
  - 业务模块应尽量通过这些服务访问平台功能，而非自己重复实现

- HomeView / HomeViewModel
  - 平台默认首页（控制 WebServer 启停、展示状态）
  - 不直接耦合具体业务，仅负责宿主层的状态和入口

- JoSystemWindowsService
  - Windows 服务宿主，负责在服务模式下启动 WebServer

---

## 3. 业务模块接口与注册机制（Controllers + Services + Repository）

文件位置：`Services/Hosting/WebServer.cs`

```csharp
app.UseRouting();
app.UseEndpoints(e =>
{
    e.MapControllers(); // 平台内置与业务 Web API 控制器
});
```

- 平台通过 Controllers 暴露所有对外 Web API
- 每个业务模块使用「Controller + Service + Repository」模式实现自己的接口和内部逻辑

---

## 4. 标准业务模块示例（Controller + Service + Repository）

业务模块对外的 Web API 推荐以 MVC 控制器形式存在，放在 `Controllers/Modules/Sample/SampleController.cs` 等位置，这样可自动被 Swagger 发现：

```csharp
[ApiController]
[Route("api/sample")]
public class SampleController : ControllerBase
{
    [HttpGet("hello")]
    public IActionResult Hello()
    {
        // 调用业务服务
        var dto = _sampleService.GetHello();
        return Ok(dto);
    }
}
```

对应的 Service / Repository 可放在：

- `Services/Modules/Sample/SampleService.cs`
- `Repositories/Modules/Sample/SampleRepository.cs`

- 控制器负责 HTTP 协议和 API 契约（输入/输出模型）
- Service 负责业务规则与组合逻辑
- Repository 负责数据库 / 外部系统访问

---

## 5. 客户级拆分与交付策略

### 5.1 平台解决方案（JoSystem.sln）

- 内容：
  - 平台核心工程（JoSystem）
  - 内部通用模块（Services/ViewModels/Views/Helpers/Controllers 等）
  - 示例业务控制器（如 `Controllers/Modules/Samples/SampleController`）
- 作用：
  - 公司内部开发与维护
  - 编译产出 DLL / EXE 供业务项目引用
- 交付：
  - 一般不向客户提供源码，只以二进制形式随安装包交付

### 5.2 客户解决方案（CustomerA.sln / CustomerB.sln）

- 内容：
  - CustomerX.Api（Web API 或 Class Library）：实现客户自有 Controllers / Services / Repository
  - CustomerX.Host（可选）：如需要专用启动器或额外桌面入口
- 引用：
  - 引用 JoSystem 平台 DLL / NuGet 包
  - 通过常规 ASP.NET Core Startup/Program 配置将客户 Controller 注册进路由
- 交付：
  - 完整源码交付给客户
  - 平台 DLL 作为依赖一起提供，但不含平台源码

---

## 6. 团队开发约定（建议）

- 平台层代码规范：
  - 禁止直接写客户特定逻辑（如 “客户A”、“某项目” 等字样）
  - 新增通用能力时优先考虑：是否可以通过 IAppModule 扩展实现

- 业务模块开发规范：
  - 一切与具体客户、具体业务相关的逻辑，统一按 Controllers + Services + Repository 模式实现
  - Web 接口统一挂在 `/api/{moduleId}/...` 路径下，避免冲突
  - 如需前端/WPF 视图，优先使用平台提供的服务（ConfigService、LogService 等）

- 交付规范：
  - 对外交付源码：仅包括 CustomerX.sln 对应的业务项目
  - 平台部分仅交付编译产物和必要的接口文档（如本文件和 README）

---

通过上述架构，JoSystem 可以作为一套可复用的“桌面 + Hosting + 服务模式平台”，在其之上按客户拆分业务模块：

- 平台核心模块：版本统一，由平台团队内部维护，通过 DLL / 包形式对外提供
- 业务模块：按客户独立仓库与解决方案管理，可交付源码、可定制扩展。

---

## 7. 内部业务模块在 WPF / HTML / API 的结构建议

- 总体原则：
  - 平台 Core：只放所有客户共用的视图、视图模型、服务和页面
  - Modules：不管是 .NET Core API、WPF 视图，还是 HTML 页面，只要是业务模块，一律放各自的 Modules 区域
  - 同一业务模块在三层（API / WPF / HTML）使用一致的模块名，便于未来拆分到客户项目

- WPF 模块结构建议：
  - 平台视图：
    - `Views/Core/MainWindow.xaml`
    - `Views/Core/HomeView.xaml`
    - `Views/Core/SettingsView.xaml`
    - `Views/Core/LogView.xaml`
  - 业务视图：
    - `Views/Modules/Order/OrderListView.xaml`
    - `Views/Modules/Order/OrderDetailView.xaml`
    - `Views/Modules/Customer/CustomerListView.xaml`
  - ViewModel 对应：
    - `ViewModels/Core/*`
    - `ViewModels/Modules/Order/OrderListViewModel.cs`
    - `ViewModels/Modules/Customer/CustomerListViewModel.cs`

- .NET Core API 模块结构（Hosting 层）：
  - 平台核心：
    - `Services/Hosting/WebServer.cs`
    - `Services/Hosting/ApiController.cs`（兼容扩展点，可选）
    - `Controllers/*`（平台内置与业务 Web API 控制器）
  - 业务模块：
    - 业务 Web API 控制器位于 `Controllers/Modules/{ModuleName}/*`
    - 对应的 Service / Repository 可位于 `Services/Modules/{ModuleName}/*`、`Repositories/Modules/{ModuleName}/*`

- HTML 前端模块结构建议：
  - 平台通用页面：
    - `WebPages/index.html`（平台首页、登录页）
    - 可扩展 `WebPages/core/settings.html` 等通用设置页面
  - 业务模块页面：
    - `WebPages/modules/order/index.html`
    - `WebPages/modules/order/detail.html`
    - `WebPages/modules/customer/index.html`
  - 建议前端路由风格与 API 对齐：
    - API：`/api/order/...`、`/api/customer/...`
    - 页面：`/order/...`、`/customer/...`

- 这样划分的好处：
  - 平台 Core 保持干净稳定，方便独立发版与复用
  - 每个业务模块在 WPF / HTML / API 三层都可“成套”迁移到 CustomerX.sln 中
  - 后续给客户拆分仓库时，可以按模块目录整体移动，风险可控，结构清晰。
