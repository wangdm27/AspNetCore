# 06 · Scheduler 定时任务

> 项目：`AspNetCore.Scheduler/AspNetCore.Scheduler.csproj`
> 命名空间：`AspNetCore.Scheduler`
> 依赖：`Hangfire.Core 1.8.14`、`Hangfire.PostgreSql 1.20.10`、`Hangfire.AspNetCore 1.8.14`、`Npgsql`（由 Hangfire.PostgreSql 传递）
> 目标框架：`net10.0`
> SDK：`Microsoft.NET.Sdk.Worker` + `FrameworkReference Microsoft.AspNetCore.App`

## 1. 模块职责

基于 Hangfire 的定时任务执行主机，独立可运行。提供：

- **复合 host**：Worker SDK + `ConfigureWebHostDefaults` 嵌 Kestrel，单进程既跑 Hangfire Job server 又暴露 Dashboard HTTP 端点。
- **PostgreSQL 持久化**：独立库存储 Job 元数据、状态、历史；server 崩溃重启后自动恢复未完成 Job。
- **自动建库**：配置开关驱动，首启自动 `CREATE DATABASE`，Hangfire schema 由 storage 自动建。
- **Dashboard**：任务监控、手动触发、查看历史与失败 Job，端口 `5300`。
- **示例 Job**：心跳（每分钟）、日志清理占位（每日 02:00），演示 DI 解析与周期注册。
- **限重试**：全局 `AutomaticRetryAttribute { Attempts = 3 }`，覆盖 Hangfire 默认 10 次重试。

## 2. 目录结构

```
AspNetCore.Scheduler/
├── AspNetCore.Scheduler.csproj        # Microsoft.NET.Sdk.Worker + FrameworkReference AspNetCore.App
├── Program.cs                          # 复合 host 入口 (CreateDefaultBuilder + build 前建库)
├── appsettings.json                    # Hangfire 段 + 连接串
├── appsettings.Development.json
├── Infrastructure/
│   ├── HangfireDbInitializer.cs        # 自动建库 (连系统库 postgres 检查+CREATE)
│   └── Extensions/
│       ├── SchedulerWebHostExtensions.cs      # ConfigureSchedulerWebHost (Kestrel+Dashboard)
│       ├── SchedulerServiceExtensions.cs      # AddSchedulerHangfire + UseSchedulerRecurringJobs
│       └── DashboardAuthorizationFilter.cs    # Dashboard 授权扩展点 (生产接入)
├── Jobs/
│   ├── ISchedulerJob.cs                # Job 抽象
│   ├── HeartbeatJob.cs                 # 示例: 每分钟心跳日志
│   └── LogCleanupJob.cs                # 示例: 每日 02:00 清理占位
└── Properties/
```

## 3. 配置选项 `appsettings.json`

`Hangfire` 段：

| 键 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `QueueName` | `string` | `default` | worker 消费的队列名 |
| `WorkerCount` | `int` | `4` | 并发 worker 数 |
| `DashboardPath` | `string` | `/hangfire` | Dashboard 路径 |
| `DashboardAllowAnonymous` | `bool` | `true` | 开发免授权；`false` 走 `DashboardAuthorizationFilter` |
| `AutoCreateDatabase` | `bool` | — | `true` 时首启自动建目标库 |
| `AdminConnectionString` | `string` | — | 连系统库 `postgres` 的连接串，用于建库 |

`ConnectionStrings`：

| 键 | 说明 |
| --- | --- |
| `HangfirePostgreSql` | Hangfire 目标库连接串（独立库 `AspNetCoreHangfireDb`，与应用业务库隔离） |

```json
{
  "ConnectionStrings": {
    "HangfirePostgreSql": "Host=localhost;Port=5432;Database=AspNetCoreHangfireDb;Username=postgres;Password=123456;SSL Mode=Disable;"
  },
  "Hangfire": {
    "QueueName": "default",
    "WorkerCount": 4,
    "DashboardPath": "/hangfire",
    "DashboardAllowAnonymous": true,
    "AutoCreateDatabase": true,
    "AdminConnectionString": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=123456;SSL Mode=Disable;"
  }
}
```

## 4. 复合 host 设计（A3）

Worker SDK 默认无 HTTP。Dashboard 需 Kestrel。采用复合 host：

- `Host.CreateDefaultBuilder(args)` 返回 `IHostBuilder`。
- `ConfigureSchedulerWebHost()` → `ConfigureWebHostDefaults` 嵌 Kestrel，`UseUrls("http://localhost:5300")`，`UseHangfireDashboard`。
- `AddSchedulerHangfire()` → `ConfigureServices` 注册 Hangfire storage + server + Jobs。

> **陷阱**：.NET 10 `ConfigureWebHostDefaults` 仅接 `IHostBuilder`，不接 `IHostApplicationBuilder`。`HostApplicationBuilder` 不可 cast 到 `IHostBuilder`。故 Program 用 `Host.CreateDefaultBuilder`（返回 `IHostBuilder`）而非 `Host.CreateApplicationBuilder`。这是 .NET 非 Web SDK 嵌 Web host 的标准复合模式。

## 5. 自动建库 `HangfireDbInitializer`

PostgreSQL 不支持 `CREATE DATABASE IF NOT EXISTS`，且 DDL 不支持参数化库名。`EnsureDatabaseAsync` 流程：

1. `AutoCreateDatabase != true` → 直接返回（跳过，库需手动建）。
2. 从 `HangfirePostgreSql` 连接串正则提取目标库名。
3. 连 `AdminConnectionString`（系统库 `postgres`）。
4. `SELECT 1 FROM pg_database WHERE datname = @db` 检查目标库存在性（参数化，安全）。
5. 不存在 → 库名正则校验 `^[A-Za-z0-9_]+$`（防注入，CREATE DATABASE 不支持参数化标识符）→ `CREATE DATABASE "name"`。
6. Hangfire schema（`Hangfire.*` 表）由 `UsePostgreSqlStorage` 首启自动建，initializer 不管 schema。

> Program 用独立 `ConfigurationBuilder` 读配置给 initializer，避免 host build 前调 `hostBuilder.Build()`（build 后 builder 不可复用）。配置源与 host 同源（appsettings + env + args）。

## 6. Hangfire 注册 `SchedulerServiceExtensions`

### 6.1 `AddSchedulerHangfire`

- `GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 })`：全局限重试 3 次。
- `AddHangfire`：`SetDataCompatibilityLevel(Version_180)` + `UseSimpleAssemblyNameTypeSerializer` + `UseRecommendedSerializerSettings` + `UsePostgreSqlStorage(o => o.UseNpgsqlConnection(conn))`。
- `AddHangfireServer`：`Queues` + `WorkerCount` 来自配置。
- Jobs scoped 注册（`HeartbeatJob`、`LogCleanupJob`），Hangfire `JobActivator` 默认解析 DI。

### 6.2 `UseSchedulerRecurringJobs`

host Build 后调用，注册周期任务。**必须用 DI 版 `IRecurringJobManager`**，不可用静态 `RecurringJob`。

> **陷阱**：静态 `RecurringJob.AddOrUpdate` 依赖 `JobStorage.Current`，在 `AddHangfire` 注册完成前调用抛 `InvalidOperationException: Current JobStorage instance has not been initialized`。DI 版 `IRecurringJobManager` 在 host Build 后解析，storage 已就绪。Job 用 `Job.FromExpression<T>(x => x.RunAsync())`（`Hangfire.Common`）构造可序列化表达式。

```csharp
var jobs = app.GetRequiredService<IRecurringJobManager>();
jobs.AddOrUpdate("heartbeat",
    Job.FromExpression<HeartbeatJob>(x => x.RunAsync()), Cron.Minutely(), tz, q);
jobs.AddOrUpdate("log-cleanup",
    Job.FromExpression<LogCleanupJob>(x => x.RunAsync()), Cron.Daily(2), tz, q);
```

> Hangfire 1.8：带 `queue` 的 `AddOrUpdate` 重载均标 `CS0618`（迁 2.0 提示），无非过时替代，已 `#pragma warning disable CS0618` 临时抑制，2.0 升级时移除。

## 7. Dashboard `SchedulerWebHostExtensions`

`ConfigureSchedulerWebHost`：

- `ConfigureWebHostDefaults` → `UseUrls("http://localhost:5300")`（避开 Api 默认 5000/5001）。
- `UseHangfireDashboard(path, DashboardOptions{ Authorization })`：
  - `DashboardAllowAnonymous == true` → 空过滤器数组（开发免授权）。
  - 否则 → `DashboardAuthorizationFilter`（占位，生产接入 JWT claims / 白名单）。

## 8. Jobs

`ISchedulerJob`：`Task RunAsync(CancellationToken ct = default)`。

| Job | Cron | 说明 |
| --- | --- | --- |
| `HeartbeatJob` | `Cron.Minutely()` | 每分钟写心跳日志，演示最小 Job |
| `LogCleanupJob` | `Cron.Daily(2)` | 每日 02:00，占位实现 |

Job 用主构造函数注入 `ILogger<T>`，DI 容器解析。

## 9. 错误处理

- Job 抛异常 → Hangfire 按 `AutomaticRetryAttribute { Attempts = 3 }` 指数退避重试。
- 重试耗尽 → 标记 failed，持久化到 PG `Hangfire.State` 表，Dashboard 可见、可手动重试/删除。
- server 崩溃重启 → 持久化队列保证未完成 Job 自动恢复。

## 10. 使用方式

### 10.1 启动

```bash
dotnet run --project AspNetCore.Scheduler
```

首次启动（`AutoCreateDatabase=true`）：

1. `HangfireDbInitializer` 连 `postgres` 系统库 → 建 `AspNetCoreHangfireDb`。
2. `UsePostgreSqlStorage` 在目标库自动建 Hangfire schema。
3. server 启动，worker 消费 `default` 队列。
4. `UseSchedulerRecurringJobs` 注册 `heartbeat`（每分钟）+ `log-cleanup`（每日 02:00）周期任务。

### 10.2 访问 Dashboard

浏览器打开 `http://localhost:5300/hangfire`：

- **Recurring Jobs**：看周期任务列表与下次触发时间。
- **Jobs / Retries / Failed**：历史执行、重试、失败 Job。
- 手动触发周期任务（点 job → Trigger now）。

### 10.3 新增定时任务

1. `Jobs/` 下新建类，实现 `ISchedulerJob`（或任意有 `RunAsync` 的类）：

```csharp
public class MyJob(ILogger<MyJob> log) : ISchedulerJob
{
    public Task RunAsync(CancellationToken ct = default)
    {
        log.LogInformation("my job run");
        return Task.CompletedTask;
    }
}
```

2. `AddSchedulerHangfire` 内 `services.AddScoped<MyJob>();` 注册。
3. `UseSchedulerRecurringJobs` 内 `jobs.AddOrUpdate("my-job", Job.FromExpression<MyJob>(x => x.RunAsync()), Cron.Hourly(), tz, q);`。

### 10.4 触发一次性任务

注入 `IBackgroundJobClient`（DI，非静态 `BackgroundJob`）：

```csharp
var client = app.Services.GetRequiredService<IBackgroundJobClient>();
client.Enqueue<MyJob>(x => x.RunAsync());
```

### 10.5 验证

- `dotnet run` 后看控制台 `heartbeat tick at ...` 每分钟输出。
- Dashboard 见 `heartbeat` 触发记录。
- PG 查 `AspNetCoreHangfireDb` 见 `Hangfire.*` 表。

## 11. 已知限制与后续事项

- **NU1903**：Hangfire 传递依赖 `Newtonsoft.Json 11.0.1` 有已知漏洞，升 Hangfire 版本才能修。
- **CS0618**：带 `queue` 的 `AddOrUpdate` 重载在 1.8 全标过时，临时 `#pragma` 抑制，2.0 迁移。
- **Dashboard 授权**：`DashboardAuthorizationFilter` 为占位，生产须置 `DashboardAllowAnonymous=false` 并接入实际授权。
- **建库权限**：`AdminConnectionString` 账号需 `CREATEDB` 权限；无权限时 `AutoCreateDatabase` 失败，需手动建库后置 `false`。
- **单进程**：server + Dashboard 同进程同端口。需独立扩展 worker 时，可另起进程只跑 server（去掉 Dashboard），共享同一 PG 存储。
