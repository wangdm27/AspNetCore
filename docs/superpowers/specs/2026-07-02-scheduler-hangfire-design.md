# Hangfire 定时任务项目设计

日期: 2026-07-02
状态: 已确认设计

## 目标

新建 `AspNetCore.Scheduler` 定时任务执行项目,使用 Hangfire 框架,作为解决方案独立可运行主机。

## 决策摘要

| 维度 | 决策 |
|------|------|
| 项目形态 | 独立可运行主机项目 (非纯库) |
| 主机 SDK | `Microsoft.NET.Sdk.Worker` |
| Dashboard | 启用,复合 host (Kestrel) 同进程暴露 |
| 框架形态 | A3: Worker SDK + `ConfigureWebHostDefaults` 嵌 Kestrel |
| 存储 | PostgreSQL,独立库 `AspNetCoreHangfireDb` |
| NuGet | Hangfire.Core / Hangfire.PostgreSql / Hangfire.AspNetCore |
| 示例任务 | 带两个 (HeartbeatJob / LogCleanupJob) |

## 架构 — A3 复合 host

Worker SDK 默认无 HTTP。Dashboard 需 Kestrel。采用复合 host:

- `Host.CreateApplicationBuilder()` (Worker SDK) 起通用 host。
- `.ConfigureWebHostDefaults(...)` 嵌 Kestrel,挂 `UseHangfireDashboard`。
- `AddHangfireServer` 起 worker 池消费队列。

单进程既跑后台 Job server 又暴露 Dashboard HTTP 端点。.NET 标准复合 host 模式。

## 项目结构

```
AspNetCore.Scheduler/
├── AspNetCore.Scheduler.csproj        # Microsoft.NET.Sdk.Worker, net10.0
├── Program.cs                          # 复合 host 入口
├── appsettings.json
├── appsettings.Development.json
├── Infrastructure/
│   └── Extensions/
│       ├── SchedulerServiceExtensions.cs   # AddSchedulerHangfire + UseSchedulerRecurringJobs
│       └── DashboardAuthorizationFilter.cs # Dashboard 授权扩展点(开发免授权)
├── Jobs/
│   ├── ISchedulerJob.cs                # Job 抽象
│   ├── HeartbeatJob.cs                 # 示例: 每分钟心跳日志
│   └── LogCleanupJob.cs                # 示例: 每日 02:00 清理占位
└── Properties/
    └── launchSettings.json
```

加入 `AspNetCore.slnx`。

## 依赖 — csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hangfire.Core" Version="1.8.14" />
    <PackageReference Include="Hangfire.PostgreSql" Version="1.20.10" />
    <PackageReference Include="Hangfire.AspNetCore" Version="1.8.14" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

`FrameworkReference Microsoft.AspNetCore.App` 提供 `ConfigureWebHostDefaults` + `UseHangfireDashboard`。`Microsoft.Extensions.Hosting` 由 Worker SDK 传递提供,无需显式引用 (NU1510)。

## 配置 — appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Hangfire": "Warning"
    }
  },
  "ConnectionStrings": {
    "HangfirePostgreSql": "Host=localhost;Port=5432;Database=AspNetCoreHangfireDb;Username=postgres;Password=123456;SSL Mode=Disable;"
  },
  "Hangfire": {
    "QueueName": "default",
    "WorkerCount": 4,
    "DashboardPath": "/hangfire",
    "DashboardAllowAnonymous": true
  },
  "AllowedHosts": "*"
}
```

- `HangfirePostgreSql`: 独立库连接串,与 Api 业务库 `AspNetCoreDb` 隔离。
- Hangfire PostgreSql storage 首启自动建 schema,无需手动迁移。
- `DashboardAllowAnonymous`: 开发免授权;生产置 false 走 `DashboardAuthorizationFilter`。

## Program.cs

```csharp
using AspNetCore.Scheduler.Infrastructure;
using AspNetCore.Scheduler.Infrastructure.Extensions;

namespace AspNetCore.Scheduler;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 独立读配置 (与 host 同源)
        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // build 前确保 Hangfire 库存在 (AutoCreateDatabase=true 时)
        await HangfireDbInitializer.EnsureDatabaseAsync(cfg);

        var hostBuilder = Host.CreateDefaultBuilder(args);
        hostBuilder.ConfigureSchedulerWebHost();
        hostBuilder.AddSchedulerHangfire();
        var host = hostBuilder.Build();
        host.Services.UseSchedulerRecurringJobs();
        await host.RunAsync();
    }
}
```

`Host.CreateDefaultBuilder` 返回 `IHostBuilder` (非 `HostApplicationBuilder`)。原因:`ConfigureWebHostDefaults` 在 .NET 10 仅接 `IHostBuilder`,不接 `IHostApplicationBuilder`。故用 `CreateDefaultBuilder` 复合 host 模式。

独立 `ConfigurationBuilder` 读配置给 initializer:避免在 host build 前调 `hostBuilder.Build()`(build 后 builder 不可复用)。配置源与 host 同源 (appsettings + env + args)。

## 自动建库 — HangfireDbInitializer

PostgreSQL 不支持 `CREATE DATABASE IF NOT EXISTS`,且 DDL 不支持参数化库名。故:

1. 读 `Hangfire:AdminConnectionString` (连系统库 `postgres`)。
2. `SELECT 1 FROM pg_database WHERE datname = @db` 检查目标库存在性 (参数化,安全)。
3. 不存在则校验库名 (仅 `[A-Za-z0-9_]+`,防注入) → `CREATE DATABASE "name"`。
4. Hangfire schema (Hangfire.* 表) 由 `UsePostgreSqlStorage` 首启自动建,无需 initializer 处理。

配置:
```json
"Hangfire": {
  "AutoCreateDatabase": true,
  "AdminConnectionString": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=123456;SSL Mode=Disable;"
}
```

`AdminConnectionString`: 连系统库 `postgres` 的连接串,用于建目标库。`AutoCreateDatabase=false` 或缺省则跳过(库需手动建)。Npgsql 由 `Hangfire.PostgreSql` 传递依赖提供,无需显式引用。

`ConfigureSchedulerWebHost`:
- `ConfigureWebHostDefaults` 起 Kestrel,`UseUrls("http://localhost:5300")`。
- 端口 5300 避开 Api 默认 5000/5001。
- `UseHangfireDashboard(path, DashboardOptions{Authorization})`。

## SchedulerServiceExtensions

```csharp
public static IHostBuilder AddSchedulerHangfire(this IHostBuilder builder)
{
    builder.ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;
        var hf = cfg.GetSection("Hangfire");
        var conn = cfg.GetConnectionString("HangfirePostgreSql")
            ?? throw new InvalidOperationException("HangfirePostgreSql 连接串缺失");

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 });

        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(conn)));

        services.AddHangfireServer(opt =>
        {
            opt.Queues = new[] { hf["QueueName"] ?? "default" };
            opt.WorkerCount = hf.GetValue<int?>("WorkerCount") ?? 4;
        });

        services.AddScoped<HeartbeatJob>();
        services.AddScoped<LogCleanupJob>();
    });
    return builder;
}

public static void UseSchedulerRecurringJobs(this IServiceProvider app)
{
    var hf = app.GetRequiredService<IConfiguration>().GetSection("Hangfire");
    var q = hf["QueueName"] ?? "default";
    var tz = TimeZoneInfo.Utc;

    // 用 IRecurringJobManager (DI,非静态 RecurringJob) — 静态 API 在 JobStorage 注册前调会抛异常。
    var jobs = app.GetRequiredService<IRecurringJobManager>();

    // Hangfire 1.8: 带 queue 的 AddOrUpdate 重载均标 CS0618 (迁 2.0 提示),无非过时替代,临时抑制。
#pragma warning disable CS0618
    jobs.AddOrUpdate("heartbeat",
        Job.FromExpression<HeartbeatJob>(x => x.RunAsync()), Cron.Minutely(), tz, q);
    jobs.AddOrUpdate("log-cleanup",
        Job.FromExpression<LogCleanupJob>(x => x.RunAsync()), Cron.Daily(2), tz, q);
#pragma warning restore CS0618
}
```

用 `IRecurringJobManager` (DI) 而非静态 `RecurringJob`。原因:静态 API 在 `IServiceCollection.AddHangfire` 注册 JobStorage 前调用会抛 `InvalidOperationException: Current JobStorage instance has not been initialized`。DI 版在 host Build 后解析,storage 已就绪。`Job.FromExpression<T>` 构造可序列化的 job 表达式。

## Jobs

```csharp
public interface ISchedulerJob
{
    Task RunAsync(CancellationToken ct = default);
}

public class HeartbeatJob(ILogger<HeartbeatJob> log) : ISchedulerJob
{
    public Task RunAsync(CancellationToken ct = default)
    {
        log.LogInformation("heartbeat tick at {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}

public class LogCleanupJob(ILogger<LogCleanupJob> log) : ISchedulerJob
{
    public Task RunAsync(CancellationToken ct = default)
    {
        log.LogInformation("log cleanup placeholder run");
        return Task.CompletedTask;
    }
}
```

## DashboardAuthorizationFilter

```csharp
public class DashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // 生产: 接实际授权 (JWT claims / 固定白名单)
        // 开发: 由 DashboardAllowAnonymous=true 跳过此过滤器
        return false;
    }
}
```

占位。开发环境 `DashboardAllowAnonymous=true` 时 `ConfigureSchedulerWebHost` 用空过滤器数组,跳过此 filter。

## 错误处理

- 全局 `AutomaticRetryAttribute { Attempts = 3 }`:Job 抛异常按指数退避重试 3 次。
- 失败 Job 持久化到 PG `Hangfire.State` 表,Dashboard 可见可手动重试/删除。
- Hangfire server 崩溃后重启自动恢复未完成 Job (持久化队列保证)。

## 测试

不写单元测试 (Hangfire 为基础设施层)。手测路径:

1. `dotnet run --project AspNetCore.Scheduler`
2. 访问 `http://localhost:5300/hangfire` 看 Dashboard。
3. 看 `heartbeat` 每分钟触发 + `log-cleanup` 注册项。
4. 查 PG `AspNetCoreHangfireDb` 见 Hangfire schema 表。
