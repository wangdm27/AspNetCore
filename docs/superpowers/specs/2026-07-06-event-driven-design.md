# 事件驱动项目设计

日期: 2026-07-06
分支: `codex/refactor-rabbitmq-library-for-stability-and-performance`
状态: 已确认设计

## 目标

解决方案已有三块基建:`AspNetCore.RabbitMq`(publisher/outbox/consumer 基类)、`AspNetCore.Scheduler`(定时主机)、`AspNetCore.Api`(业务入口)。缺事件驱动消费主机:让 Api 等业务方发布领域事件,独立主机订阅处理(解耦/异步/可独立扩缩)。

本设计新增 `AspNetCore.Events` 契约库 + `AspNetCore.EventDriven` 消费主机,并在 `AspNetCore.Api` 加发布端点。

## 决策摘要

| 维度 | 决策 |
|------|------|
| 事件契约共享 | 新建公共库 `AspNetCore.Events`,只放事件 record + `IEventBus` 抽象 + 命名约定 |
| 事件总线封装 | `Events` 库定义 `IEventBus`(`PublishAsync` + `EnqueueAsync` Outbox 版);`RabbitMq` 库提供实现,按约定算 exchange/routingKey/queue |
| 发布端 | `Api` 加示例端点发布 `UserCreatedEvent` |
| 消费主机 | `AspNetCore.EventDriven`(Worker SDK),独立后台进程 |
| 解耦边界 | Api 引用 RabbitMq(基建库)+ Events(契约库);Api **不引用** EventDriven 消费主机 |
| 命名约定 | exchange/routingKey/queue 均由 `typeof(TEvent).Name` 派生,两端须一致 |

## 1. 背景与目标

现有基建足以发/收消息,但没有"事件"这一层抽象:发布方需直接拼 exchange/routingKey,消费方需手写拓扑声明。引入事件驱动后:

1. **契约共享**:发布方与消费方引用同一 `AspNetCore.Events` 库,事件类型强类型共享。
2. **解耦**:Api 发布事件,不关心谁消费;EventDriven 主机订阅事件,不关心谁发布。Api 不引用 EventDriven,可独立部署/扩缩。
3. **异步**:事件发布即返回,消费在独立进程;失败重试由 Outbox + 消费者 nack 重入承担。

## 2. 用户决策

- **事件契约共享**:新建公共库 `AspNetCore.Events`,只放事件 record + `IEventBus` 抽象 + 命名约定。
- **事件总线封装**:`Events` 库定义 `IEventBus`(`PublishAsync` 直发 + `EnqueueAsync` Outbox 版),`RabbitMq` 实现按约定算 exchange/routingKey/queue。
- **发布端**:`Api` 加示例端点发布 `UserCreatedEvent`。

## 3. 核心矛盾与解法(关键)

决策 1 原述"Api 只引用 Events 不引用 RabbitMq",但 Api 发事件需 `IEventBus` 实现,实现必然依赖 RabbitMq 库(`IRabbitMqPublisher`/`IRabbitMqOutbox`)。矛盾。

**解法(方案 C,用户确认):**

- `RabbitMqEventBus` 实现 + `AddRabbitMqEventBus` 扩展放 `AspNetCore.RabbitMq` 库。
- Api 引用 RabbitMq(基建库)+ Events(契约库)。
- Api **不引用** EventDriven 消费主机——这是真正要守的解耦边界。
- `Events` 库保持零 RabbitMq 依赖。

决策 1 真实意图"发布方/消费方解耦"由"Api 不引用 EventDriven"达成,而非"Api 不引用 RabbitMq"。RabbitMq 是基建库(像 Logging/DI 一样),引用它不破坏解耦;引用消费主机才会把发布方与具体消费逻辑耦合。

## 4. 命名约定

所有事件路由由事件类型名派生,两端各自配置前缀,前缀须一致:

| 维度 | 计算规则 | 示例 |
|------|----------|------|
| exchange | `ExchangePrefix + typeof(TEvent).Name` | `evt.UserCreatedEvent` |
| routingKey | `typeof(TEvent).Name` | `UserCreatedEvent` |
| queue | `QueuePrefix + typeof(TEvent).Name` | `q.UserCreatedEvent` |
| exchange type | 默认 `direct` | `direct` |

- `EventBusNaming.ForPublish<TEvent>` → `(exchange, routingKey)`。
- `EventBusNaming.ForConsume<TEvent>` → `(exchange, routingKey, queue)`。
- 两端 `EventBusOptions` 须一致(同前缀),否则路由不匹配。

## 5. 项目结构

### 5.1 AspNetCore.Events(类库 net10.0,零 RabbitMq 依赖)

```
AspNetCore.Events/
├── AspNetCore.Events.csproj
├── IEvent.cs                  # 事件标记接口
├── IEventBus.cs               # 事件总线抽象(PublishAsync + EnqueueAsync)
├── EventBusOptions.cs         # ExchangePrefix / QueuePrefix
├── EventBusNaming.cs          # ForPublish<T> / ForConsume<T> 静态计算
└── Events/
    └── UserCreatedEvent.cs    # 示例事件 record
```

`IEventBus` 抽象:

```csharp
public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class, IEvent;
    ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class, IEvent;
}
```

`Events` 库零 RabbitMq 依赖:`IEventBus` 是抽象,实现在 `RabbitMq` 库。

### 5.2 AspNetCore.RabbitMq(改)

- csproj 加 `Events` 引用。
- 新增 `RabbitMqEventBus.cs`:`internal sealed`,实现 `IEventBus`;构造注入 `IRabbitMqPublisher` + `IRabbitMqOutbox` + `EventBusOptions`。
  - `PublishAsync` → `publisher.PublishAsync(exchange, routingKey, @event, confirm: true, ct)`。
  - `EnqueueAsync` → `outbox.EnqueueAsync(exchange, routingKey, @event, ct)`。
- `ServiceCollectionExtensions` 加 `AddRabbitMqEventBus(Action<EventBusOptions>?)`。

```csharp
internal sealed class RabbitMqEventBus(
    IRabbitMqPublisher publisher,
    IRabbitMqOutbox outbox,
    EventBusOptions options) : IEventBus
{
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : class, IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(options);
        return publisher.PublishAsync(exchange, routingKey, @event, confirm: true, ct);
    }

    public ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : class, IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(options);
        return outbox.EnqueueAsync(exchange, routingKey, @event, ct);
    }
}
```

### 5.3 AspNetCore.EventDriven(Worker net10.0)

引用 `Events` + `RabbitMq`,`PackageReference Microsoft.Extensions.Hosting` + `Configuration.Binder`。

```
AspNetCore.EventDriven/
├── AspNetCore.EventDriven.csproj        # Microsoft.NET.Sdk.Worker, net10.0
├── Program.cs                            # Host.CreateDefaultBuilder + AddEventDriven + RunAsync
├── appsettings.json                      # RabbitMq + EventBus 节
├── appsettings.Development.json
├── EventDrivenConsumerBase.cs            # 继承 RabbitMqConsumerBase<TEvent>,按约定 override 拓扑
├── RabbitMqConsumerHostedService.cs      # IHostedService 包装消费者生命周期
├── Consumers/
│   └── UserCreatedEventConsumer.cs       # 示例消费者
└── Infrastructure/
    └── Extensions/
        └── EventDrivenServiceExtensions.cs  # AddEventDriven(this IHostBuilder)
```

`EventDrivenConsumerBase<TEvent>`:继承 `RabbitMqConsumerBase<TEvent>`,`where TEvent : class, IEvent`,按约定 override `Queue`/`Exchange`/`RoutingKey`/`ExchangeType`。

### 5.4 AspNetCore.Api(改)

- csproj 加 `Events` + `RabbitMq` 引用。
- `AddBusinessModules` 末尾加 `AddUnifiedRabbitMq` + `AddRabbitMqEventBus`。
- `appsettings` 加 `RabbitMq` + `EventBus` 节。
- `Controllers/DemoEventsController`:注入 `IEventBus`,`POST /api/demo/publish-user-created` 直发 + `/api/demo/enqueue-user-created` Outbox,匿名。

## 6. 消费者托管(关键缺口修复)

`RabbitMqConsumerBase` 是 `IAsyncDisposable` 但**非 `IHostedService`**(原有缺口,Test3 手动 `StartAsync` 且停时漏 `DisposeAsync`)。

`RabbitMqConsumerHostedService<TConsumer>` 包装消费者生命周期:

- 泛型约束 `where TConsumer : IRabbitMqConsumer, IAsyncDisposable`。
- `StartAsync` → `consumer.StartAsync`。
- `StopAsync` → `consumer.DisposeAsync`。

`RabbitMqConsumerBase` 停止靠 `DisposeAsync`:先 `BasicCancelAsync` 再归还租约,对重复释放幂等(内部置空 `_lease`)。消费者注册 **Singleton**(长租通道,与池生命周期一致)。

```csharp
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

> 注:`TConsumer` 须加 `IAsyncDisposable` 约束才能在 `StopAsync`/`DisposeAsync` 调 `DisposeAsync`——这是泛型约束的关键点,`IRabbitMqConsumer` 本身不含 `IAsyncDisposable`。

## 7. DI 注册

### 7.1 EventDriven — AddEventDriven(this IHostBuilder)

```csharp
public static IHostBuilder AddEventDriven(this IHostBuilder builder)
{
    builder.ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;
        services.AddUnifiedRabbitMq(o => cfg.GetSection("RabbitMq").Bind(o));
        services.AddRabbitMqEventBus(o => cfg.GetSection("EventBus").Bind(o));

        // 消费者 Singleton(长租通道)
        services.AddSingleton<UserCreatedEventConsumer>();
        services.AddHostedService<RabbitMqConsumerHostedService<UserCreatedEventConsumer>>();
    });
    return builder;
}
```

对齐 `Scheduler.AddSchedulerHangfire` 的 `IHostBuilder` 风格(扩展挂在 `IHostBuilder` 上,内部 `ConfigureServices`)。

### 7.2 Api — AddBusinessModules

`AddBusinessModules` 末尾加 `AddUnifiedRabbitMq` + `AddRabbitMqEventBus`。**仅发布,不注册消费者**。

## 8. Program 形态

EventDriven 用 `Host.CreateDefaultBuilder`(`IHostBuilder`,对齐 Scheduler)。.NET 10 `ConfigureWebHostDefaults` 仅接 `IHostBuilder`,不接 `IHostApplicationBuilder`,故 Worker 类项目统一用 `CreateDefaultBuilder`。

无 Kestrel/Dashboard,纯后台 Worker。

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

> 踩坑:Worker SDK 默认**不含** `Microsoft.Extensions.Hosting`(`Host.CreateDefaultBuilder`)与 `Configuration.Binder`(`GetValue`/`Bind`),须显式 `PackageReference`。与 Scheduler 不同——Scheduler 引 `FrameworkReference Microsoft.AspNetCore.App` 传递提供 Hosting;EventDriven 纯 Worker 不引 ASP.NET Core,故须显式包引用。

## 9. 风险与已知限制

- **毒消息循环**:`RabbitMqConsumerBase` catch 块 `BasicNackAsync(requeue:true)`,处理抛异常时无限重入,DLX 不捕获 nack。库层面限制,本项目不修。
- **Outbox 内存存储**:`InMemoryRabbitMqOutboxStore` 进程重启丢未投递消息,生产需换持久存储。
- **EventBusOptions 两端各自实例**:配置不一致则路由不匹配,须保持 `EventBus` 节一致(同 `ExchangePrefix`/`QueuePrefix`)。
- **RabbitMq 库重构已提交**(0 错 0 警),本方案基于现状续做。

## 验证

1. `dotnet build AspNetCore.Events` 编译通过、0 错 0 警。
2. `dotnet build AspNetCore.RabbitMq` 编译通过、0 错 0 警。
3. `dotnet build AspNetCore.EventDriven` 编译通过、0 错 0 警。
4. `dotnet build AspNetCore.Api` 编译通过(既有 NU1903 警告与本次无关)。
5. `dotnet build AspNetCore.slnx` 全解决方案通过(4 个 NU1903 警告均既有)。
6. 端到端:RabbitMQ → EventDriven 主机运行 → Api POST `/api/demo/publish-user-created` → EventDriven 日志见消费。
