# 08 · Logging 日志库

> 项目：`AspNetCore.Logging/AspNetCore.Logging.csproj`
> 命名空间：`AspNetCore.Logging`（+ `AspNetCore.Logging.Enrichers`）
> 依赖：`Serilog.AspNetCore 9.0.0`、`Serilog.Sinks.Console/File/Seq`、`Serilog.Enrichers.Environment/Thread`、`Serilog.Extensions.Logging 9.0.0`、`Microsoft.Extensions.* 10.0.0`
> 目标框架：`net10.0`
> SDK：`Microsoft.NET.Sdk`

## 1. 模块职责

基于 Serilog 封装的统一日志库，对齐 `AspNetCore.Redis` / `AspNetCore.RabbitMq` 库风格（POCO Options + `AddXxx`/`UseXxx` 扩展 + 中文 XML doc）。提供：

- **多 Sink**：Console（开发实时）+ File（按应用分目录、按日滚动，兜底/归档）+ Seq（主查询入口，结构化检索）
- **TraceId 全链路贯通**：Api 请求 → RabbitMq 消息头（W3C `traceparent`）→ EventDriven 消费者，Seq 按 TraceId 串联整条业务链路
- **Enrichment**：`ApplicationName`、`TraceId`、`SpanId`、`UserId`、`TenantId`、`MachineName`、`ThreadId`
- **三宿主统一接入**：Api（`WebApplicationBuilder`）/ Scheduler（`IHostBuilder`）/ EventDriven（`IHostBuilder`）

涉及项目：

- **新增 `AspNetCore.Logging`**：日志库本体
- **改动 `AspNetCore.RabbitMq`**：新增 `RabbitMqTracing`（`traceparent` 注入/提取），`Publisher` 发布时注入，`ConsumerBase` 收消息时恢复 Activity
- **改动 `AspNetCore.Api`**：新增 `HttpContextUserContextProvider`（`IUserContextProvider` 实现），`AddBusinessModules` 注册
- **三宿主 `Program.cs`**：接入 `UseAspNetCoreLogging` + `appsettings` 加 `LoggingLib` 节

## 2. 目录结构

```
AspNetCore.Logging/
├── AspNetCore.Logging.csproj          # Microsoft.NET.Sdk, net10.0, Serilog 包引用
├── LoggingOptions.cs                  # 顶层配置（ApplicationName/MinimumLevel/Sinks/Enrichment）
├── LoggingSinksOptions.cs             # Console/File/Seq 开关与参数
├── LoggingEnrichmentOptions.cs        # TraceId/UserContext/MachineName/ThreadId 开关
├── IUserContextProvider.cs            # 用户上下文抽象（零 ASP.NET Core 依赖关键）
├── LoggerConfigurationExtensions.cs   # ConfigureAspNetCoreLogging: 按 options 配置 sinks/enrichers
├── HostBuilderLoggingExtensions.cs    # UseAspNetCoreLogging（IHostBuilder / IHostApplicationBuilder 重载）
└── Enrichers/
    ├── ActivityTraceIdEnricher.cs     # 读 Activity.Current.TraceId/SpanId
    └── HttpContextUserEnricher.cs     # 读 IUserContextProvider 的 UserId/TenantId
```

## 3. 核心设计

### 3.1 引擎选 Serilog

不自造日志引擎。Serilog 桥接 ASP.NET Core `ILogger`，业务代码用 `ILogger<T>` 无感。封装统一接入与配置抽象，屏蔽 Serilog 配置细节。

### 3.2 Options POCO + Action 绑定

对齐 `RedisOptions` / `AddRedis` 模式：`LoggingOptions` POCO + 默认值 + `Action<LoggingOptions>` 绑定，**不走 `IOptions<T>`**。三宿主各自从 `appsettings` 的 `LoggingLib` 节绑定。

### 3.3 零 ASP.NET Core 框架依赖（关键决策）

Logging 库是 `Microsoft.NET.Sdk` 类库（非 Web SDK），**不引用 `Microsoft.AspNetCore.App` 共享框架**。但 `HttpContextUserEnricher` 需读 `UserId`/`TenantId`——通过 `IUserContextProvider` 抽象解耦：

- Logging 库定义 `IUserContextProvider` 接口（`UserId` / `TenantId`）
- `HttpContextUserEnricher` 依赖此接口（静态 holder），**不碰 `HttpContext` 类型**
- Web 宿主（Api）实现 `IUserContextProvider` 包装 `IHttpContextAccessor`，注册到 DI
- Worker 宿主不注册，enricher 拿 `null` 跳过

> Logger 在 DI 之前创建，enricher 无法构造注入；用 `HttpContextEnricherInitializer`（`IHostedService`）在 host 启动后从 DI 取 `IUserContextProvider` 绑定到静态 holder。

## 4. TraceId 全链路贯通

### 4.1 原理

ASP.NET Core 默认启用 W3C TraceContext，每个请求自动创建 `Activity`（TraceId）。`ILogger` 经 `ActivityTraceIdEnricher` 输出 `TraceId`。跨 RabbitMq 进程时，靠消息头 `traceparent` 传递：

```
Api 请求 (Activity T1)
  └─ Publisher 发布，Inject traceparent=T1 到消息头
       └─ MQ 投递
            └─ ConsumerBase 收消息，Extract traceparent=T1，StartActivity(parent=T1)
                 └─ HandleAsync 内 Activity.Current.TraceId = T1
                      └─ ILogger 输出 TraceId=T1
```

Seq 按 `TraceId=T1` 过滤，拉出 Api 发布 + EventDriven 消费整条链路。

### 4.2 `RabbitMqTracing`

`AspNetCore.RabbitMq/RabbitMqTracing.cs`，`internal static`，**纯 BCL（`System.Diagnostics`），零 Logging 库依赖**：

- `Inject(IDictionary headers)`：读 `Activity.Current`，构造 W3C `traceparent`（`00-{traceId32hex}-{spanId16hex}-01`）写入头
- `ExtractAndStartActivity(IDictionary? headers)`：从头解析 `traceparent` 为 `ActivityContext`，`ActivitySource.StartActivity(Consumer, parentContext)` 创建延续 Activity

### 4.3 改动点

| 文件 | 改动 |
| --- | --- |
| `RabbitMqPublisher.PublishRawAsync` | `props?.Invoke` 后调 `RabbitMqTracing.Inject(properties.Headers)`。所有发布路径（直接发布 + Outbox dispatcher 发布）都经此点 |
| `RabbitMqConsumerBase.ReceivedAsync` | 回调内 `using var activity = RabbitMqTracing.ExtractAndStartActivity(ea.BasicProperties.Headers)`，包裹 `HandleAsync`。**`HandleAsync` 签名不变，子类零改动** |

### 4.4 Outbox 路径（已知限制）

`EnqueueAsync` → Outbox 内存存储 → dispatcher 异步发布。store 不存 `traceparent`，dispatcher 发布时 `Activity.Current` 已非原始请求 Activity，**TraceId 断裂**。本期不处理，后续需扩 `RabbitMqOutboxMessage` schema 存 `traceparent`。

## 5. Enrichment

| Enricher | 来源 | 配置开关 | 生效宿主 |
| --- | --- | --- | --- |
| `ActivityTraceIdEnricher` | `Activity.Current.TraceId/SpanId` | `EnableTraceId` | 全部 |
| `HttpContextUserEnricher` | `IUserContextProvider.UserId/TenantId` | `EnableUserContext` | 仅 Web（Api） |
| `WithMachineName` | `Serilog.Enrichers.Environment` | `EnableMachineName` | 全部 |
| `WithThreadId` | `Serilog.Enrichers.Thread` | `EnableThreadId` | 全部 |
| `ApplicationName` | `WithProperty` | 始终 | 全部 |

## 6. DI 接入

`HostBuilderLoggingExtensions` 两个重载：

| 重载 | 宿主 | 机制 |
| --- | --- | --- |
| `UseAspNetCoreLogging(this IHostApplicationBuilder)` | Api（`WebApplicationBuilder`） | `builder.Configuration` 读 `LoggingLib`，`Log.Logger=logger`，`builder.Logging.AddSerilog` |
| `UseAspNetCoreLogging(this IHostBuilder)` | Scheduler / EventDriven | `ConfigureServices(ctx)` 内读 `ctx.Configuration`，`Log.Logger=logger`，`UseSerilog(dispose:true)` 用全局 `Log.Logger` |

Api 额外注册 `IUserContextProvider` 实现（`HttpContextUserContextProvider`），供 `HttpContextUserEnricher` 启动后绑定。

## 7. 配置选项（`appsettings.json` `LoggingLib` 节）

| 键 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ApplicationName` | `string` | `app` | File 目录分区 + Seq source |
| `MinimumLevel` | `enum` | `Information` | 全局最低级别 |
| `Sinks:EnableConsole` | `bool` | `true` | 控制台 |
| `Sinks:EnableFile` | `bool` | `true` | 文件（按日滚动） |
| `Sinks:EnableSeq` | `bool` | `true` | Seq |
| `Sinks:SeqUrl` | `string` | `http://localhost:5341` | Seq 地址 |
| `Sinks:FileBasePath` | `string` | `logs` | 文件根目录，实际 `{FileBasePath}/{ApplicationName}/logyyyyMMdd.log` |
| `Sinks:FileRetainedFileCountLimit` | `int?` | `14` | 保留文件数 |
| `Sinks:FileSizeLimitBytes` | `long?` | `10MB` | 单文件大小上限 |
| `Enrichment:EnableTraceId` | `bool` | `true` | TraceId/SpanId |
| `Enrichment:EnableUserContext` | `bool` | `true` | UserId/TenantId（仅 Web 生效） |
| `Enrichment:EnableMachineName` | `bool` | `true` | 机器名 |
| `Enrichment:EnableThreadId` | `bool` | `true` | 线程 ID |

三宿主 `ApplicationName`：`Api` / `Scheduler` / `EventDriven`。Scheduler、EventDriven 的 `EnableUserContext=false`（Worker 无 HttpContext）。

## 8. Seq 部署与查询

**Windows 安装包**（无需 docker）：

1. https://datalust.co/seq 下载 Seq Windows installer
2. 双击安装，作为 Windows 服务自启
3. 浏览器开 `http://localhost:5341`
4. Seq 控制台设 retention（如 7 天 / 容量上限）自动清理

数据存 Seq 安装目录（压缩格式），只能用 Seq Web UI/API 查。备份直接备份该目录。

查询：

- **按字段过滤**：`UserId=123`、`Level=Error`、`ApplicationName=Api`
- **按 TraceId 串联**：点 `TraceId` 值，拉出 Api→MQ→EventDriven 全链路
- **按 ApplicationName 区分来源**

## 9. 端到端验证

1. 装 Seq（§8），开 `http://localhost:5341`
2. 启 RabbitMQ（`localhost:5672`）
3. `dotnet run --project AspNetCore.EventDriven`
4. `dotnet run --project AspNetCore.Api`
5. `curl -X POST http://localhost:5175/api/demo/publish-user-created`
6. Seq 按 `TraceId` 过滤：见 Api 发布日志 + EventDriven 消费日志**同 TraceId**
7. `logs/Api/`、`logs/EventDriven/` 有按日滚动文件

## 10. 已知限制

- **Outbox 路径 TraceId 断裂**（§4.4）
- **Worker 宿主无 UserContext**：Scheduler/EventDriven 的 `UserId`/`TenantId` 为空
- **毒消息循环**：继承 `RabbitMqConsumerBase`，详见 [07-EventDriven-事件驱动.md](./07-EventDriven-事件驱动.md) §14
- **Seq 未装时**：sink 重试连接不崩，但 Seq 无数据；Console/File 正常

## 11. 演进路径（C 方案）

当前 B 方案：本地 Console + File + Seq。后续 **C 方案**：日志走 RabbitMq 异步落 ES/PG，复用 `AspNetCore.RabbitMq` 的发布确认 / 死信 / Outbox，削峰解耦 + 集中化检索。在 `LoggerConfigurationExtensions` 加 RabbitMq sink 即可，配置开关 `EnableRabbitMq`。与 B 方案不冲突，可叠加。
