# 07 · EventDriven 事件驱动

> 项目：`AspNetCore.Events/AspNetCore.Events.csproj` + `AspNetCore.EventDriven/AspNetCore.EventDriven.csproj`
> 命名空间：`AspNetCore.Events` / `AspNetCore.EventDriven`
> 依赖：`Microsoft.Extensions.Hosting 10.0.0`、`Microsoft.Extensions.Configuration.Binder 10.0.0`（仅 EventDriven 主机）；Events 库零 NuGet 依赖
> 目标框架：`net10.0`
> SDK：Events 为 `Microsoft.NET.Sdk`；EventDriven 为 `Microsoft.NET.Sdk.Worker`

## 1. 模块职责

事件驱动链路由三层组成，分属两个新增项目与两个改动项目：

- **Events 契约库**（`AspNetCore.Events`）：纯接口 + record 的事件契约库，零 RabbitMq 依赖。定义 `IEvent` 标记接口、`IEventBus` 抽象（`PublishAsync` + `EnqueueAsync`）、`EventBusOptions` 命名约定、`EventBusNaming` 命名计算器。发布方与消费方共享此契约。
- **EventDriven 消费主机**（`AspNetCore.EventDriven`）：基于 Worker SDK 的后台消费进程。自动启停消费者、按 `EventBusNaming` 约定声明 AMQP 拓扑（exchange/queue/binding）。业务方不引用此项目。
- **RabbitMq 库改动**（`AspNetCore.RabbitMq`）：新增 `RabbitMqEventBus`（`internal sealed`，`IEventBus` 的 RabbitMq 实现）+ `ServiceCollectionExtensions.AddRabbitMqEventBus` 扩展。加 `ProjectReference AspNetCore.Events`。
- **Api 改动**（`AspNetCore.Api`）：csproj 加 Events + RabbitMq 引用；`AddBusinessModules` 末尾追加 `AddUnifiedRabbitMq` + `AddRabbitMqEventBus`；appsettings 加 RabbitMq + EventBus 节；新增 `Controllers/DemoEventsController.cs` 演示发布端点。

> **解耦边界**：发布方（Api）引用 Events + RabbitMq，**不引用 EventDriven**；消费主机不被业务方引用。这是事件驱动发布/消费解耦的核心约束。

## 2. 目录结构

### 2.1 `AspNetCore.Events`

```
AspNetCore.Events/
├── AspNetCore.Events.csproj        # Microsoft.NET.Sdk, 零 PackageReference, 零 RabbitMq 依赖
├── IEvent.cs                       # 事件标记接口
├── IEventBus.cs                    # PublishAsync<TEvent> + EnqueueAsync<TEvent> 抽象, where TEvent : IEvent
├── EventBusOptions.cs              # ExchangePrefix("evt.") / QueuePrefix("q.") / UseTypeNameAsRoutingKey(true) / ExchangeType("direct")
├── EventBusNaming.cs               # ForPublish<TEvent> -> (exchange,routingKey); ForConsume<TEvent> -> (exchange,routingKey,queue)
└── Events/
    └── UserCreatedEvent.cs         # record: UserId / UserName / Email / CreatedAt
```

### 2.2 `AspNetCore.EventDriven`

```
AspNetCore.EventDriven/
├── AspNetCore.EventDriven.csproj                          # Microsoft.NET.Sdk.Worker, 引用 Events + RabbitMq
├── Program.cs                                              # Host.CreateDefaultBuilder + AddEventDriven + RunAsync
├── appsettings.json                                        # RabbitMq 节 + EventBus 节
├── appsettings.Development.json
├── EventDrivenConsumerBase.cs                              # 继承 RabbitMqConsumerBase<TEvent>, 按约定 override Queue/Exchange/RoutingKey
├── RabbitMqConsumerHostedService.cs                       # 包装 IRabbitMqConsumer 为 IHostedService
├── Consumers/
│   └── UserCreatedEventConsumer.cs                         # 示例, HandleAsync 写日志
└── Infrastructure/
    └── Extensions/
        └── EventDrivenServiceExtensions.cs                  # AddEventDriven(this IHostBuilder)
```

## 3. 三层架构

| 层 | 项目 | 职责 | RabbitMq 依赖 |
| --- | --- | --- | --- |
| 契约 | `AspNetCore.Events` | `IEvent` + `IEventBus` 抽象 + record 事件 + 命名约定 | 零 |
| 基建 | `AspNetCore.RabbitMq` | `IEventBus` 的 RabbitMq 实现 + 通道池/发布者/Outbox | `RabbitMQ.Client 7.2.0` |
| 消费 | `AspNetCore.EventDriven` | Worker 主机，自动启停消费者，按约定声明拓扑 | 经 RabbitMq 库 |

依赖方向：

- 发布方（Api）引用 `Events` + `RabbitMq`，从 DI 取 `IEventBus`，不感知 AMQP。
- 消费主机（EventDriven）引用 `Events` + `RabbitMq`，注册消费者为 Singleton + HostedService 包装。
- 业务方不引用 `EventDriven`——消费主机作为独立进程运行，发布方与消费方仅通过 broker 与共享契约解耦。

## 4. `IEventBus` 抽象

`AspNetCore.Events/IEventBus.cs`：

```csharp
public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IEvent;
    ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IEvent;
}
```

- **`PublishAsync<TEvent>`**（直发）：立即投递到 broker，走 `IRabbitMqPublisher.PublishAsync(..., confirm: true)`。实时性优先，失败时抛异常给调用方。
- **`EnqueueAsync<TEvent>`**（Outbox）：先入发件箱，由后台调度器 `RabbitMqOutboxDispatcher` 可靠投递，含重试退避（指数退避，封顶 `RetryMaxDelay`）+ 死信兜底（达 `MaxRetryCount` 转 DLX）。可靠性优先，调用方不阻塞等待 broker。

调用方仅依赖 `IEventBus`，不碰 `exchange` / `routingKey` / AMQP 细节。

## 5. 命名约定

`EventBusNaming` 按事件类型名自动算 exchange / routingKey / queue，发布端与消费端共用同一计算器，确保路由匹配。

| 项 | 公式 | 示例（`UserCreatedEvent`） |
| --- | --- | --- |
| `exchange` | `ExchangePrefix + typeof(TEvent).Name` | `evt.UserCreatedEvent` |
| `routingKey` | `typeof(TEvent).Name`（`UseTypeNameAsRoutingKey=true`） | `UserCreatedEvent` |
| `queue` | `QueuePrefix + typeof(TEvent).Name` | `q.UserCreatedEvent` |
| `exchange type` | `EventBusOptions.ExchangeType` | `direct` |

- 发布端用 `EventBusNaming.ForPublish<TEvent>(opts)` → `(exchange, routingKey)`。
- 消费端用 `EventBusNaming.ForConsume<TEvent>(opts)` → `(exchange, routingKey, queue)`。
- 两端 `EventBusOptions` 各自从配置读，**配置节必须一致**，否则路由不匹配。

## 6. `RabbitMqEventBus` 实现

`AspNetCore.RabbitMq/RabbitMqEventBus.cs`，`internal sealed`：

```csharp
internal sealed class RabbitMqEventBus : IEventBus
{
    public RabbitMqEventBus(IRabbitMqPublisher publisher, IRabbitMqOutbox outbox, EventBusOptions opts) { ... }

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(_opts);
        return _publisher.PublishAsync(exchange, routingKey, @event, confirm: true, cancellationToken: ct);
    }

    public ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(_opts);
        return _outbox.EnqueueAsync(exchange, routingKey, @event, cancellationToken: ct);
    }
}
```

构造注入 `IRabbitMqPublisher` + `IRabbitMqOutbox` + `EventBusOptions`。`PublishAsync` 调 publisher 直发（confirm=true），`EnqueueAsync` 调 outbox 入箱。屏蔽 AMQP，调用方不碰 exchange/routingKey。

## 7. 消费者托管（关键缺口修复）

### 7.1 原有缺口

`RabbitMqConsumerBase<T>` 是 `IAsyncDisposable` 但**非 `IHostedService`**。`Test3` 示例手动调 `StartAsync`，停止时漏 `DisposeAsync`（通道租约不归还，池泄漏）。

### 7.2 `RabbitMqConsumerHostedService<TConsumer>`

`AspNetCore.EventDriven/RabbitMqConsumerHostedService.cs` 把消费者包装成 `IHostedService`：

- 泛型约束 `where TConsumer : IRabbitMqConsumer, IAsyncDisposable`。
- `StartAsync` → `consumer.StartAsync(ct)`。
- `StopAsync` → `consumer.DisposeAsync()`（`RabbitMqConsumerBase` 停止靠 `DisposeAsync`：先 `BasicCancelAsync` 停止消费，再归还通道租约）。
- 自身也实现 `IAsyncDisposable`，`DisposeAsync` 兜底幂等调用（`RabbitMqConsumerBase.DisposeAsync` 内部判 `_lease null` 直接 return，对重复释放幂等）。

> `RabbitMqConsumerBase` 无 `StopAsync` 方法，停止语义由 `DisposeAsync` 承载。HostedService 适配此约定。

## 8. `EventDrivenConsumerBase<TEvent>`

`AspNetCore.EventDriven/EventDrivenConsumerBase.cs`，继承 `RabbitMqConsumerBase<TEvent>`：

```csharp
public abstract class EventDrivenConsumerBase<TEvent> : RabbitMqConsumerBase<TEvent>
    where TEvent : class, IEvent
{
    // 按 EventBusNaming.ForConsume<TEvent> 自动 override Queue/Exchange/RoutingKey
    // override ExchangeType 用 opts.ExchangeType
}
```

- 泛型约束 `where TEvent : class, IEvent`（`RabbitMqConsumerBase` 要求 `T : class`；`IEvent` 是标记接口）。
- 构造注入 `[FromKeyedServices("consumer")] IRabbitMqChannelPool` + `RabbitMqOptions` + `EventBusOptions`。
- 按 `EventBusNaming.ForConsume<TEvent>(_opts)` 自动 override `Queue` / `Exchange` / `RoutingKey`。
- `override ExchangeType => _opts.ExchangeType`。
- 子类只实现 `HandleAsync`，零拓扑代码。

## 9. DI 注册 `AddEventDriven`

`AspNetCore.EventDriven/Infrastructure/Extensions/EventDrivenServiceExtensions.cs`，对齐 `Scheduler.AddSchedulerHangfire` 的 `IHostBuilder` 扩展风格：

```csharp
public static IHostBuilder AddEventDriven(this IHostBuilder builder)
{
    builder.ConfigureServices((ctx, services) =>
    {
        // 1. AddUnifiedRabbitMq（从 RabbitMq 节读：连接 + 双池 + publisher + outbox + dispatcher）
        // 2. AddRabbitMqEventBus（从 EventBus 节读：命名约定）
        // 3. 消费者注册为 Singleton（长租通道，生命周期与 host 一致）
        services.AddSingleton<UserCreatedEventConsumer>();
        // 4. 每个消费者包一层 HostedService，随 host 自动启停
        services.AddHostedService<RabbitMqConsumerHostedService<UserCreatedEventConsumer>>();
    });
    return builder;
}
```

> **新增消费者**：追加两行注册——`services.AddSingleton<XxxEventConsumer>();` + `services.AddHostedService<RabbitMqConsumerHostedService<XxxEventConsumer>>();`。

消费者必须注册为 **Singleton**：长租通道，生命周期与 host 一致，不可 Scoped/Transient（Scoped 会被 DI 容器释放，通道租约被提前归还）。

## 10. `Program`

`AspNetCore.EventDriven/Program.cs`：

```csharp
var hostBuilder = Host.CreateDefaultBuilder(args);  // 返回 IHostBuilder
hostBuilder.AddEventDriven();
var host = hostBuilder.Build();
await host.RunAsync();
```

用 `Host.CreateDefaultBuilder`（返回 `IHostBuilder`），对齐 Scheduler 风格（.NET 10 `ConfigureWebHostDefaults` 仅接 `IHostBuilder`）。无 Kestrel、无 Dashboard，纯后台 Worker。

## 11. Api 发布端

### 11.1 `AddBusinessModules` 末尾追加

`AspNetCore.Api/Infrastructure/Extensions/ServiceCollectionExtensions.cs`，`AddBusinessModules` 末尾：

```csharp
// RabbitMQ 基建 + 事件总线（发布端）。Api 仅发布，不注册任何消费者。
services.AddUnifiedRabbitMq(opt => { /* 从 RabbitMq 节读 */ });
services.AddRabbitMqEventBus(opt => { /* 从 EventBus 节读 */ });
```

Api 仅发布，不注册任何消费者（消费者在 EventDriven 主机注册）。

### 11.2 `DemoEventsController`

`AspNetCore.Api/Controllers/DemoEventsController.cs`，注入 `IEventBus`（不碰 `IRabbitMqPublisher`），两个匿名端点：

| 端点 | 方法 | 说明 |
| --- | --- | --- |
| `POST /api/demo/publish-user-created` | `PublishAsync` | 直发，立即投递到 broker（confirm） |
| `POST /api/demo/enqueue-user-created` | `EnqueueAsync` | Outbox，入箱后由调度器可靠投递 |

## 12. 配置选项 `appsettings.json`

### 12.1 `RabbitMq` 节

| 键 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `HostName` | `string` | `localhost` | RabbitMQ 主机 |
| `Port` | `int` | `5672` | AMQP 端口 |
| `UserName` | `string` | `guest` | 用户名 |
| `Password` | `string` | `guest` | 密码 |
| `VirtualHost` | `string` | `/` | vhost |
| `PrefetchCount` | `int` | `10` | 消费者 QoS 预取 |
| `ChannelPoolSize` | `int` | `16` | 发布者通道池大小 |
| `ConsumerChannelPoolSize` | `int` | `16` | 消费者通道池大小 |
| `EnableDeadLetter` | `bool` | `false` | 是否启用死信队列 |
| `DeadLetterExchange` | `string` | `""` | 死信交换机 |
| `DeadLetterRoutingKey` | `string` | `""` | 死信路由键 |
| `DeadLetterQueue` | `string` | `""` | 死信队列 |
| `MaxRetryCount` | `int` | `5` | Outbox 最大重试次数 |
| `RetryBaseDelaySeconds` | `int` | `5` | Outbox 重试退避基数（秒） |
| `RetryMaxDelayMinutes` | `int` | `5` | Outbox 重试退避封顶（分钟） |

### 12.2 `EventBus` 节

| 键 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ExchangePrefix` | `string` | `evt.` | 交换机名前缀 |
| `QueuePrefix` | `string` | `q.` | 队列名前缀 |
| `ExchangeType` | `string` | `direct` | 交换机类型 |

> Api 的 `appsettings.json` RabbitMq 节为子集（HostName/Port/UserName/Password/VirtualHost/ChannelPoolSize），EventBus 节同上。两端 EventBus 节必须一致。

## 13. 端到端验证

1. 启 RabbitMQ：`localhost:5672`，`guest`/`guest`。Docker：`docker run -d --name rmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`。
2. `dotnet run --project AspNetCore.EventDriven` → 日志 `Consumer UserCreatedEventConsumer started`。
3. `dotnet run --project AspNetCore.Api`（新终端，默认端口 `5175`）。
4. `curl -X POST http://localhost:5175/api/demo/publish-user-created` → EventDriven 日志收到 `UserCreatedEvent`（直发，即时）。
5. `curl -X POST http://localhost:5175/api/demo/enqueue-user-created` → 延迟（Outbox 调度间隔默认 3 秒）后 EventDriven 收到。
6. （可选）RabbitMQ 管理界面 `http://localhost:15672`（`guest`/`guest`）见 `evt.UserCreatedEvent` exchange + `q.UserCreatedEvent` queue。
7. `Ctrl+C` 停 EventDriven → `Consumer stopped`（优雅停止，`DisposeAsync` 先 `BasicCancel` 再归还通道租约）。

## 14. 已知限制

- **毒消息循环**：`RabbitMqConsumerBase` catch 块 `BasicNackAsync(requeue: true)`，处理抛异常时无限重入（DLX 不捕获 nack）。库层面限制，本项目不修。
- **Outbox 内存存储**：`InMemoryRabbitMqOutboxStore` 进程重启丢未投递消息，生产需换持久存储（PG/EF）。
- **`EventBusOptions` 两端各自实例**：Api 与 EventDriven 各自从配置读，若配置不一致则路由不匹配，须保持 `EventBus` 节一致。
