# 事件驱动项目 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 `AspNetCore.Events` 契约库 + `AspNetCore.EventDriven` 消费主机,`AspNetCore.RabbitMq` 库加 `IEventBus` 实现,`AspNetCore.Api` 加发布端点,补齐消费者托管生命周期缺口。

**Architecture:** Events 库(零 RabbitMq 依赖)定义 `IEvent`/`IEventBus`/`EventBusOptions`/`EventBusNaming` + 示例事件;RabbitMq 库实现 `RabbitMqEventBus`(注入 publisher+outbox+options)并提供 `AddRabbitMqEventBus`;EventDriven(Worker)用 `EventDrivenConsumerBase<TEvent>` 继承 `RabbitMqConsumerBase<TEvent>` 按约定算拓扑,`RabbitMqConsumerHostedService<TConsumer>` 包装消费者为 `IHostedService`;Api 引 Events+RabbitMq 加发布端点。

**Tech Stack:** .NET 10.0、`AspNetCore.RabbitMq`(已重构完成,0 错 0 警)、Worker SDK(`Microsoft.NET.Sdk.Worker`)、`Microsoft.Extensions.Hosting` + `Configuration.Binder`(Worker SDK 默认不含,须显式 PackageReference)。

**Design spec:** `docs/superpowers/specs/2026-07-06-event-driven-design.md`

---

## 验证方式说明(偏离 TDD 默认)

本仓库无测试项目(Test/Test2/Test3 均为 console Exe),且 RabbitMQ 集成需 live broker。对齐 RabbitMq 重构 plan 的策略:验证 = `dotnet build` 编译检查点(每任务 0 错) + 端到端手测(任务 7)。每个任务结尾跑构建并确认 0 错误。

构建命令统一按项目:`dotnet build <Project>/<Project>.csproj`

注意:构建输出含中文(可能乱码),看末尾 `0 个错误` / `0 Error` 即通过。既有 NU1903 警告(4 个,均与本次无关)不阻断。

---

## 文件结构

| 文件 | 责任 | 操作 |
| --- | --- | --- |
| `AspNetCore.Events/AspNetCore.Events.csproj` | 契约库项目文件 | 新建 |
| `AspNetCore.Events/IEvent.cs` | 事件标记接口 | 新建 |
| `AspNetCore.Events/IEventBus.cs` | 事件总线抽象 | 新建 |
| `AspNetCore.Events/EventBusOptions.cs` | 前缀配置 | 新建 |
| `AspNetCore.Events/EventBusNaming.cs` | 命名约定计算 | 新建 |
| `AspNetCore.Events/Events/UserCreatedEvent.cs` | 示例事件 | 新建 |
| `AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj` | 基建库项目文件 | 修改:加 Events 引用 |
| `AspNetCore.RabbitMq/RabbitMqEventBus.cs` | IEventBus 实现 | 新建 |
| `AspNetCore.RabbitMq/ServiceCollectionExtensions.cs` | DI 注册 | 修改:加 AddRabbitMqEventBus |
| `AspNetCore.EventDriven/AspNetCore.EventDriven.csproj` | 消费主机项目文件 | 新建 |
| `AspNetCore.EventDriven/EventDrivenConsumerBase.cs` | 消费者基类 | 新建 |
| `AspNetCore.EventDriven/RabbitMqConsumerHostedService.cs` | 消费者托管包装 | 新建 |
| `AspNetCore.EventDriven/Consumers/UserCreatedEventConsumer.cs` | 示例消费者 | 新建 |
| `AspNetCore.EventDriven/Infrastructure/Extensions/EventDrivenServiceExtensions.cs` | AddEventDriven | 新建 |
| `AspNetCore.EventDriven/Program.cs` | 主机入口 | 新建 |
| `AspNetCore.EventDriven/appsettings.json` | 配置 | 新建 |
| `AspNetCore.EventDriven/appsettings.Development.json` | 开发配置 | 新建 |
| `AspNetCore.Api/AspNetCore.Api.csproj` | Api 项目文件 | 修改:加 Events+RabbitMq 引用 |
| `AspNetCore.Api/.../BusinessModuleExtensions.cs` | AddBusinessModules | 修改:加 AddUnifiedRabbitMq+AddRabbitMqEventBus |
| `AspNetCore.Api/appsettings.json` | Api 配置 | 修改:加 RabbitMq+EventBus 节 |
| `AspNetCore.Api/Controllers/DemoEventsController.cs` | 发布端点 | 新建 |
| `AspNetCore.slnx` | 解决方案 | 修改:加两项目 |
| `docs/07-EventDriven.md` | 项目文档 | 新建 |
| `docs/00-项目总览.md` | 总览 | 修改:加事件驱动条目 |

---

## Task 1: 新建 AspNetCore.Events 契约库

**Files:**
- Create: `AspNetCore.Events/AspNetCore.Events.csproj`
- Create: `AspNetCore.Events/IEvent.cs`
- Create: `AspNetCore.Events/IEventBus.cs`
- Create: `AspNetCore.Events/EventBusOptions.cs`
- Create: `AspNetCore.Events/EventBusNaming.cs`
- Create: `AspNetCore.Events/Events/UserCreatedEvent.cs`

- [ ] **Step 1: 创建 csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

零 RabbitMq 依赖,零 PackageReference。纯契约库。

- [ ] **Step 2: 创建 IEvent.cs**

```csharp
namespace AspNetCore.Events;

/// <summary>
/// 领域事件标记接口
/// </summary>
public interface IEvent { }
```

- [ ] **Step 3: 创建 EventBusOptions.cs**

```csharp
namespace AspNetCore.Events;

/// <summary>
/// 事件总线命名约定配置
/// </summary>
/// <remarks>
/// 两端(发布方/消费方)须配置一致的前缀,否则路由不匹配。
/// </remarks>
public sealed class EventBusOptions
{
    /// <summary>
    /// 交换机名前缀,拼接 typeof(TEvent).Name
    /// </summary>
    public string ExchangePrefix { get; set; } = "evt.";

    /// <summary>
    /// 队列名前缀,拼接 typeof(TEvent).Name
    /// </summary>
    public string QueuePrefix { get; set; } = "q.";
}
```

- [ ] **Step 4: 创建 EventBusNaming.cs**

```csharp
namespace AspNetCore.Events;

/// <summary>
/// 事件命名约定计算
/// </summary>
public static class EventBusNaming
{
    /// <summary>
    /// 发布侧:返回 (exchange, routingKey)
    /// </summary>
    public static (string Exchange, string RoutingKey) ForPublish<TEvent>(EventBusOptions options)
        where TEvent : class, IEvent
    {
        var name = typeof(TEvent).Name;
        return (options.ExchangePrefix + name, name);
    }

    /// <summary>
    /// 消费侧:返回 (exchange, routingKey, queue)
    /// </summary>
    public static (string Exchange, string RoutingKey, string Queue) ForConsume<TEvent>(EventBusOptions options)
        where TEvent : class, IEvent
    {
        var name = typeof(TEvent).Name;
        return (options.ExchangePrefix + name, name, options.QueuePrefix + name);
    }
}
```

- [ ] **Step 5: 创建 IEventBus.cs**

```csharp
namespace AspNetCore.Events;

/// <summary>
/// 事件总线抽象
/// </summary>
/// <remarks>
/// 实现在 AspNetCore.RabbitMq 库(RabbitMqEventBus)。Events 库零 RabbitMq 依赖。
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// 直发事件(走 publisher confirm)
    /// </summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// 入 Outbox(后台调度投递,进程重启需持久存储才不丢)
    /// </summary>
    ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}
```

- [ ] **Step 6: 创建 Events/UserCreatedEvent.cs**

```csharp
namespace AspNetCore.Events.Events;

/// <summary>
/// 示例事件:用户创建
/// </summary>
public sealed record UserCreatedEvent(Guid UserId, string UserName, DateTimeOffset CreatedAt) : IEvent;
```

- [ ] **Step 7: 构建检查点**

Run: `dotnet build AspNetCore.Events/AspNetCore.Events.csproj`
Expected: **0 个错误,0 个警告**。

- [ ] **Step 8: Commit**

```bash
git add AspNetCore.Events/
git commit -m "feat(events): add AspNetCore.Events contract library (IEvent/IEventBus/Options/Naming)"
```

---

## Task 2: RabbitMq 库加 EventBus 实现

**Files:**
- Modify: `AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
- Create: `AspNetCore.RabbitMq/RabbitMqEventBus.cs`
- Modify: `AspNetCore.RabbitMq/ServiceCollectionExtensions.cs`

- [ ] **Step 1: csproj 加 Events 引用**

在 `AspNetCore.RabbitMq.csproj` 的 `<ItemGroup>` 中加:

```xml
<ProjectReference Include="..\AspNetCore.Events\AspNetCore.Events.csproj" />
```

- [ ] **Step 2: 创建 RabbitMqEventBus.cs**

```csharp
using AspNetCore.Events;

namespace AspNetCore.RabbitMq;

/// <summary>
/// IEventBus 的 RabbitMq 实现
/// </summary>
/// <remarks>
/// 按命名约定算 exchange/routingKey,委托 IRabbitMqPublisher(直发)或 IRabbitMqOutbox(入队)。
/// 实现放 RabbitMq 库(非 Events 库),保持 Events 零 RabbitMq 依赖。
/// </remarks>
internal sealed class RabbitMqEventBus(
    IRabbitMqPublisher publisher,
    IRabbitMqOutbox outbox,
    EventBusOptions options) : IEventBus
{
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(options);
        return publisher.PublishAsync(exchange, routingKey, @event, confirm: true, cancellationToken);
    }

    public ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(options);
        return outbox.EnqueueAsync(exchange, routingKey, @event, cancellationToken);
    }
}
```

> 注:`ForPublish<TEvent>` 返回 2 元组,解构为 `(exchange, routingKey)`——**不可写成 3 元组解构**(ForConsume 才是 3 元组)。

- [ ] **Step 3: ServiceCollectionExtensions 加 AddRabbitMqEventBus**

在 `ServiceCollectionExtensions.cs` 的 `AddUnifiedRabbitMq` 方法之后追加:

```csharp
        /// <summary>
        /// 注册 IEventBus(RabbitMq 实现)
        /// </summary>
        public static IServiceCollection AddRabbitMqEventBus(
            this IServiceCollection services,
            Action<EventBusOptions>? configure = null)
        {
            var options = new EventBusOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
            return services;
        }
```

并在文件顶部 using 区追加(若缺):

```csharp
using AspNetCore.Events;
```

- [ ] **Step 4: 构建检查点**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: **0 个错误,0 个警告**(库重构已完成,本任务纯增量)。

- [ ] **Step 5: Commit**

```bash
git add AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj AspNetCore.RabbitMq/RabbitMqEventBus.cs AspNetCore.RabbitMq/ServiceCollectionExtensions.cs
git commit -m "feat(rabbitmq): add RabbitMqEventBus + AddRabbitMqEventBus implementing IEventBus"
```

---

## Task 3: 新建 AspNetCore.EventDriven 消费主机

**Files:**
- Create: `AspNetCore.EventDriven/AspNetCore.EventDriven.csproj`
- Create: `AspNetCore.EventDriven/EventDrivenConsumerBase.cs`
- Create: `AspNetCore.EventDriven/RabbitMqConsumerHostedService.cs`
- Create: `AspNetCore.EventDriven/Consumers/UserCreatedEventConsumer.cs`
- Create: `AspNetCore.EventDriven/Infrastructure/Extensions/EventDrivenServiceExtensions.cs`
- Create: `AspNetCore.EventDriven/Program.cs`
- Create: `AspNetCore.EventDriven/appsettings.json`
- Create: `AspNetCore.EventDriven/appsettings.Development.json`

- [ ] **Step 1: 创建 csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\AspNetCore.Events\AspNetCore.Events.csproj" />
    <ProjectReference Include="..\AspNetCore.RabbitMq\AspNetCore.RabbitMq.csproj" />
  </ItemGroup>
</Project>
```

> **踩坑**:Worker SDK 默认**不含** `Microsoft.Extensions.Hosting`(`Host.CreateDefaultBuilder`)与 `Configuration.Binder`(`Bind`/`GetValue`),须显式 PackageReference。与 Scheduler 不同——Scheduler 引 `FrameworkReference Microsoft.AspNetCore.App` 传递提供 Hosting;EventDriven 纯 Worker 不引 ASP.NET Core,故须显式包引用。

- [ ] **Step 2: 创建 EventDrivenConsumerBase.cs**

```csharp
using AspNetCore.Events;
using AspNetCore.RabbitMq;

namespace AspNetCore.EventDriven;

/// <summary>
/// 事件驱动消费者基类:按命名约定 override 拓扑
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public abstract class EventDrivenConsumerBase<TEvent> : RabbitMqConsumerBase<TEvent>
    where TEvent : class, IEvent
{
    private readonly EventBusOptions _options;

    protected EventDrivenConsumerBase(
        [FromKeyedServices("consumer")] IRabbitMqChannelPool channelPool,
        RabbitMqOptions rabbitMqOptions,
        EventBusOptions eventBusOptions)
        : base(channelPool, rabbitMqOptions)
    {
        _options = eventBusOptions;
    }

    protected override string Queue => _options.QueuePrefix + typeof(TEvent).Name;
    protected override string Exchange => _options.ExchangePrefix + typeof(TEvent).Name;
    protected override string RoutingKey => typeof(TEvent).Name;
    protected override string ExchangeType => "direct";
}
```

> 注:`ForConsume<TEvent>` 返回 **3 元组** `(exchange, routingKey, queue)`——若用解构须写 3 变量,不可写成 2。本基类直接拼字符串,避免解构。

- [ ] **Step 3: 创建 RabbitMqConsumerHostedService.cs**

```csharp
using AspNetCore.RabbitMq;

namespace AspNetCore.EventDriven;

/// <summary>
/// 消费者托管包装:把 IAsyncDisposable 消费者适配为 IHostedService
/// </summary>
/// <remarks>
/// 修复 RabbitMqConsumerBase 非 IHostedService 的缺口(原 Test3 手动 StartAsync 且停时漏 DisposeAsync)。
/// StartAsync → consumer.StartAsync;StopAsync → consumer.DisposeAsync(先 BasicCancel 再归还租约,幂等)。
/// </remarks>
internal sealed class RabbitMqConsumerHostedService<TConsumer> : IHostedService, IAsyncDisposable
    where TConsumer : IRabbitMqConsumer, IAsyncDisposable
{
    private readonly TConsumer _consumer;

    public RabbitMqConsumerHostedService(TConsumer consumer) => _consumer = consumer;

    public Task StartAsync(CancellationToken cancellationToken) => _consumer.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken) => await _consumer.DisposeAsync();

    public ValueTask DisposeAsync() => _consumer.DisposeAsync();
}
```

> **踩坑**:`TConsumer` 须加 `IAsyncDisposable` 约束才能调 `DisposeAsync`——`IRabbitMqConsumer` 本身不含 `IAsyncDisposable`。泛型约束写 `where TConsumer : IRabbitMqConsumer, IAsyncDisposable`。

- [ ] **Step 4: 创建 Consumers/UserCreatedEventConsumer.cs**

```csharp
using AspNetCore.Events;
using AspNetCore.Events.Events;
using AspNetCore.RabbitMq;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EventDriven.Consumers;

/// <summary>
/// 示例:消费 UserCreatedEvent
/// </summary>
internal sealed class UserCreatedEventConsumer(
    [FromKeyedServices("consumer")] IRabbitMqChannelPool channelPool,
    RabbitMqOptions rabbitMqOptions,
    EventBusOptions eventBusOptions,
    ILogger<UserCreatedEventConsumer> logger)
    : EventDrivenConsumerBase<UserCreatedEvent>(channelPool, rabbitMqOptions, eventBusOptions)
{
    protected override Task HandleAsync(UserCreatedEvent message, CancellationToken ct)
    {
        logger.LogInformation("UserCreatedEvent consumed: UserId={UserId} UserName={UserName} CreatedAt={CreatedAt}",
            message.UserId, message.UserName, message.CreatedAt);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: 创建 Infrastructure/Extensions/EventDrivenServiceExtensions.cs**

```csharp
using AspNetCore.EventDriven.Consumers;
using AspNetCore.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.EventDriven.Infrastructure.Extensions;

public static class EventDrivenServiceExtensions
{
    /// <summary>
    /// 注册 EventDriven 消费主机:RabbitMq 基建 + EventBus + 消费者 + 托管服务
    /// </summary>
    public static IHostBuilder AddEventDriven(this IHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) =>
        {
            var cfg = ctx.Configuration;
            services.AddUnifiedRabbitMq(o => cfg.GetSection("RabbitMq").Bind(o));
            services.AddRabbitMqEventBus(o => cfg.GetSection("EventBus").Bind(o));

            // 消费者 Singleton(长租通道,与池生命周期一致)
            services.AddSingleton<UserCreatedEventConsumer>();
            services.AddHostedService<RabbitMqConsumerHostedService<UserCreatedEventConsumer>>();
        });
        return builder;
    }
}
```

对齐 `Scheduler.AddSchedulerHangfire` 的 `IHostBuilder` 风格(扩展挂在 `IHostBuilder` 上)。

- [ ] **Step 6: 创建 Program.cs**

```csharp
using AspNetCore.EventDriven.Infrastructure.Extensions;

namespace AspNetCore.EventDriven;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .AddEventDriven()
            .Build()
            .RunAsync();
    }
}
```

- [ ] **Step 7: 创建 appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ChannelPoolSize": 8,
    "ConsumerChannelPoolSize": 16,
    "PrefetchCount": 10,
    "EnableDeadLetter": false,
    "OutboxBatchSize": 100,
    "OutboxDispatchInterval": "00:00:05",
    "MaxRetryCount": 5,
    "RetryBaseDelay": "00:00:05",
    "RetryMaxDelay": "00:05:00",
    "PublisherConfirmTimeout": "00:00:10"
  },
  "EventBus": {
    "ExchangePrefix": "evt.",
    "QueuePrefix": "q."
  }
}
```

- [ ] **Step 8: 创建 appsettings.Development.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

- [ ] **Step 9: 构建检查点**

Run: `dotnet build AspNetCore.EventDriven/AspNetCore.EventDriven.csproj`
Expected: **0 个错误,0 个警告**。

- [ ] **Step 10: Commit**

```bash
git add AspNetCore.EventDriven/
git commit -m "feat(eventdriven): add consumer host with EventDrivenConsumerBase + HostedService wrapper"
```

---

## Task 4: Api 集成发布端

**Files:**
- Modify: `AspNetCore.Api/AspNetCore.Api.csproj`
- Modify: `AspNetCore.Api/.../BusinessModuleExtensions.cs`(含 `AddBusinessModules` 的文件)
- Modify: `AspNetCore.Api/appsettings.json`
- Create: `AspNetCore.Api/Controllers/DemoEventsController.cs`

- [ ] **Step 1: csproj 加 Events + RabbitMq 引用**

在 `AspNetCore.Api.csproj` 的 `<ItemGroup>` 中加:

```xml
<ProjectReference Include="..\AspNetCore.Events\AspNetCore.Events.csproj" />
<ProjectReference Include="..\AspNetCore.RabbitMq\AspNetCore.RabbitMq.csproj" />
```

- [ ] **Step 2: AddBusinessModules 加注册**

在 `AddBusinessModules` 方法**末尾**(`return services;` 之前)追加:

```csharp
        services.AddUnifiedRabbitMq(o => configuration.GetSection("RabbitMq").Bind(o));
        services.AddRabbitMqEventBus(o => configuration.GetSection("EventBus").Bind(o));
```

并在文件顶部 using 区追加(若缺):

```csharp
using AspNetCore.RabbitMq;
```

> Api 仅发布,不注册消费者——这是解耦边界(Api 不引用 EventDriven)。

- [ ] **Step 3: appsettings.json 加节**

在 `AspNetCore.Api/appsettings.json` 顶层加:

```json
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ChannelPoolSize": 8,
    "ConsumerChannelPoolSize": 16,
    "PrefetchCount": 10,
    "EnableDeadLetter": false,
    "OutboxBatchSize": 100,
    "OutboxDispatchInterval": "00:00:05",
    "MaxRetryCount": 5,
    "RetryBaseDelay": "00:00:05",
    "RetryMaxDelay": "00:05:00",
    "PublisherConfirmTimeout": "00:00:10"
  },
  "EventBus": {
    "ExchangePrefix": "evt.",
    "QueuePrefix": "q."
  }
```

> 两端 `EventBus` 节须一致(同 `ExchangePrefix`/`QueuePrefix`),否则路由不匹配。

- [ ] **Step 4: 创建 Controllers/DemoEventsController.cs**

```csharp
using AspNetCore.Events;
using AspNetCore.Events.Events;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers;

/// <summary>
/// 示例:事件发布端点(匿名)
/// </summary>
[ApiController]
[Route("api/demo")]
public class DemoEventsController(IEventBus eventBus) : ControllerBase
{
    /// <summary>
    /// 直发 UserCreatedEvent(走 publisher confirm)
    /// </summary>
    [HttpPost("publish-user-created")]
    public async Task<IActionResult> PublishUserCreated(CancellationToken ct)
    {
        var evt = new UserCreatedEvent(Guid.NewGuid(), $"user_{Random.Shared.Next(1000, 9999)}", DateTimeOffset.UtcNow);
        await eventBus.PublishAsync(evt, ct);
        return Ok(new { evt.UserId, evt.UserName, Mode = "Publish" });
    }

    /// <summary>
    /// 入 Outbox(后台调度投递)
    /// </summary>
    [HttpPost("enqueue-user-created")]
    public async Task<IActionResult> EnqueueUserCreated(CancellationToken ct)
    {
        var evt = new UserCreatedEvent(Guid.NewGuid(), $"user_{Random.Shared.Next(1000, 9999)}", DateTimeOffset.UtcNow);
        await eventBus.EnqueueAsync(evt, ct);
        return Ok(new { evt.UserId, evt.UserName, Mode = "Enqueue" });
    }
}
```

- [ ] **Step 5: 构建检查点**

Run: `dotnet build AspNetCore.Api/AspNetCore.Api.csproj`
Expected: **0 个错误**(既有 NU1903 警告与本次无关,不阻断)。

- [ ] **Step 6: Commit**

```bash
git add AspNetCore.Api/AspNetCore.Api.csproj AspNetCore.Api/Controllers/DemoEventsController.cs AspNetCore.Api/appsettings.json
git add AspNetCore.Api/  # 兜底(BusinessModuleExtensions 路径需按实际)
git commit -m "feat(api): add demo event publish endpoints (direct + outbox)"
```

---

## Task 5: slnx 加两项目 + 整解决方案构建

**Files:**
- Modify: `AspNetCore.slnx`

- [ ] **Step 1: slnx 加两项目**

```bash
dotnet sln AspNetCore.slnx add AspNetCore.Events/AspNetCore.Events.csproj
dotnet sln AspNetCore.slnx add AspNetCore.EventDriven/AspNetCore.EventDriven.csproj
```

- [ ] **Step 2: 整解决方案构建检查点**

Run: `dotnet build AspNetCore.slnx`
Expected: **0 个错误**(4 个 NU1903 警告均既有,与本次无关)。

- [ ] **Step 3: Commit**

```bash
git add AspNetCore.slnx
git commit -m "chore(sln): add Events + EventDriven projects to solution"
```

---

## Task 6: 文档

**Files:**
- Create: `docs/07-EventDriven.md`
- Modify: `docs/00-项目总览.md`

- [ ] **Step 1: 新建 docs/07-EventDriven.md**

写事件驱动项目文档:架构(Events 契约库 / RabbitMq 实现 / EventDriven 消费主机 / Api 发布端)、命名约定表、DI 注册说明、消费者托管缺口修复说明、配置项、风险与限制。对齐 `docs/06-Scheduler-定时任务.md` 风格。

- [ ] **Step 2: 更新 docs/00-项目总览.md`

加 `AspNetCore.Events` 与 `AspNetCore.EventDriven` 两条目到项目列表,补事件驱动架构概览段。

- [ ] **Step 3: 本 spec + 本 plan 已在 Task 前置完成**

`docs/superpowers/specs/2026-07-06-event-driven-design.md` 与 `docs/superpowers/plans/2026-07-06-event-driven.md` 已写。

- [ ] **Step 4: Commit**

```bash
git add docs/07-EventDriven.md docs/00-项目总览.md
git commit -m "docs: add event-driven project doc + update overview"
```

---

## Task 7: 端到端验证

**Files:** 无(验证)

- [ ] **Step 1: 启动 RabbitMQ broker**

需本地 RabbitMQ `localhost:5672`(guest/guest)。

- [ ] **Step 2: 启动 EventDriven 消费主机**

Run: `dotnet run --project AspNetCore.EventDriven`
Expected: 控制台启动日志,消费者 `StartAsync` 声明 `evt.UserCreatedEvent` exchange + `q.UserCreatedEvent` queue + 绑定。

- [ ] **Step 3: 启动 Api 并发事件**

另开终端:
```bash
dotnet run --project AspNetCore.Api
# 另开终端发请求
curl -X POST http://localhost:5000/api/demo/publish-user-created
curl -X POST http://localhost:5000/api/demo/enqueue-user-created
```

Expected: Api 返回 `{UserId, UserName, Mode}`。

- [ ] **Step 4: 看 EventDriven 日志**

Expected: EventDriven 控制台输出 `UserCreatedEvent consumed: UserId=... UserName=... CreatedAt=...` 两条(一条直发、一条 Outbox 投递)。

- [ ] **Step 5: 验证通过即完成**

端到端通即本计划完成。毒消息循环 / Outbox 内存存储属已知限制(spec 第 9 节),不在本计划修。

---

## Self-Review 自检结果

1. **Spec 覆盖**：
   - Events 契约库 → Task 1 ✓
   - RabbitMq 实现 IEventBus + AddRabbitMqEventBus → Task 2 ✓
   - EventDriven 消费主机(EventDrivenConsumerBase + HostedService + Consumer + Extensions + Program + appsettings)→ Task 3 ✓
   - Api 发布端点(直发 + Outbox)→ Task 4 ✓
   - 消费者托管缺口修复(RabbitMqConsumerHostedService)→ Task 3 Step 3 ✓
   - 解耦边界(Api 引 RabbitMq+Events,不引 EventDriven)→ Task 4 ✓
   - slnx 集成 → Task 5 ✓
   - 文档 → Task 6 ✓
   - 端到端 → Task 7 ✓

2. **踩坑标注**：
   - Worker SDK 默认不含 Hosting/Configuration.Binder(Task 3 Step 1)✓
   - ForConsume 返回 3 元组不可写 2(Task 3 Step 2)✓
   - TConsumer 须加 IAsyncDisposable 约束(Task 3 Step 3)✓
   - EventBus 两端须一致(Task 4 Step 3)✓

3. **类型一致性**：`IEventBus.PublishAsync<TEvent>(event, ct)` / `EnqueueAsync<TEvent>(event, ct)` 在 Events 定义、RabbitMq 实现、Api 调用一致 ✓;`EventDrivenConsumerBase<TEvent> where TEvent : class, IEvent` 与 `RabbitMqConsumerBase<T> where T : class` 约束兼容 ✓;`RabbitMqConsumerHostedService<TConsumer> where TConsumer : IRabbitMqConsumer, IAsyncDisposable` 与 `UserCreatedEventConsumer`(继承链含 IAsyncDisposable)兼容 ✓。

4. **检查点策略**：每任务独立 `dotnet build` 0 错(Task 1-3 各自项目独立可绿;Task 4 既有 NU1903 警告不阻断;Task 5 整解决方案 0 错)。无中间红检查点(各项目依赖已就绪:Events 无依赖,Task 2 时 Events 已建,Task 3 时 RabbitMq 已改完,Task 4 时三者均就绪)。
