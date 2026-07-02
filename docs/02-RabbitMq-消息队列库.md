# 02 · RabbitMq 消息队列库

> 项目：`AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
> 命名空间：`AspNetCore.RabbitMq`
> 依赖：`RabbitMQ.Client 7.2.0`、`Microsoft.Extensions.* 10.x`（keyed services）
> 目标框架：`net10.0`

## 1. 模块职责

封装 RabbitMQ 客户端，提供：

- **连接管理**：单连接 + 自动恢复 + 双重检查锁。
- **双通道池**：发布者池（短租）与消费者池（长租）同类型 keyed 双实例，互不饿死。所有通道创建时开启发布确认（publisher confirm）。
- **发布者**：直发（实时）与 Outbox（可靠投递）；支持发布确认（`confirm`）、延迟投递（`delayMs`）、字符串 UTF-8 / 对象 JSON 两种序列化。
- **消费者**：抽象基类，自动声明交换机/队列/绑定与可选死信拓扑；从消费者池租用通道，`Dispose` 时先 `BasicCancel` 再归还。
- **Outbox 模式**：内存存储 + 后台调度器批量重试，指数退避 + 重试上限 + 死信兜底。

## 2. 目录结构

```
AspNetCore.RabbitMq/
├── IRabbitMqConnection.cs             # 连接抽象
├── RabbitMqConnection.cs               # 连接实现（双重检查锁）
├── IRabbitMqChannelPool.cs             # 通道池抽象 + PooledChannelLease 租约结构体（携带 tracker）
├── RabbitMqChannelPool.cs              # 通道池实现（信号量 + ConcurrentQueue，confirm channel）
├── ChannelConfirmTracker.cs            # 通道级 ack/nack 追踪器 + PublishConfirmResult 枚举
├── IRabbitMqPublisher.cs               # 发布者抽象
├── RabbitMqPublisher.cs                # 发布者实现（confirm/delayMs/string-utf8）
├── RabbitMqPublishNackedException.cs  # broker nack 拒绝异常
├── IRabbitMqConsumer.cs                # 消费者抽象
├── RabbitMqConsumerBase.cs             # 消费者基类（池租 + 自动声明 + DLX + IDisposable）
├── IRabbitMqOutbox.cs                  # Outbox 入箱抽象
├── RabbitMqOutbox.cs                   # Outbox 入箱实现
├── IRabbitMqOutboxStore.cs             # Outbox 存储抽象
├── InMemoryRabbitMqOutboxStore.cs      # 内存存储实现
├── RabbitMqOutboxMessage.cs            # Outbox 消息实体
├── RabbitMqOutboxDispatcher.cs        # 后台调度器（重试上限 + 退避 + 死信兜底）
├── RabbitMqOptions.cs                  # 配置选项
└── ServiceCollectionExtensions.cs      # DI 注册入口 AddUnifiedRabbitMq（keyed 双池）
```

## 3. 配置选项 `RabbitMqOptions`

`RabbitMqOptions.cs`：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `HostName` | `string` | `localhost` | 主机名 |
| `Port` | `int` | `5672` | 端口 |
| `UserName` / `Password` | `string` | `guest` / `guest` | 凭据 |
| `VirtualHost` | `string` | `/` | vhost |
| `PrefetchCount` | `ushort` | `10` | 消费预取数 |
| `AutomaticRecoveryEnabled` | `bool` | `true` | 自动连接恢复 |
| `TopologyRecoveryEnabled` | `bool` | `true` | 拓扑恢复 |
| `NetworkRecoveryInterval` | `TimeSpan` | `10s` | 恢复间隔 |
| `ChannelPoolSize` | `int` | `16` | 发布者通道池上限 |
| `ConsumerChannelPoolSize` | `int` | `16` | 消费者通道池上限 |
| `EnableDeadLetter` | `bool` | `false` | 消费者是否启用死信 |
| `DeadLetterExchange` | `string` | `""` | 死信交换机名 |
| `DeadLetterRoutingKey` | `string` | `""` | 死信路由键 |
| `DeadLetterQueue` | `string` | `""` | 死信队列名 |
| `DefaultMessageTTL` | `TimeSpan?` | `null` | 主队列 `x-message-ttl`，null 不设 |
| `MaxRetryCount` | `int` | `5` | Outbox 最大重试次数 |
| `RetryBaseDelay` | `TimeSpan` | `5s` | Outbox 退避基数 |
| `RetryMaxDelay` | `TimeSpan` | `5min` | Outbox 退避封顶 |
| `OutboxDispatchInterval` | `TimeSpan` | `3s` | Outbox 调度间隔 |
| `OutboxBatchSize` | `int` | `100` | Outbox 每批数量 |
| `PublisherConfirmTimeout` | `TimeSpan` | `10s` | 发布确认等待超时 |

## 4. DI 注册 `AddUnifiedRabbitMq`

`ServiceCollectionExtensions.cs`，注册 keyed 双通道池：

| 注册 | 实现 | 说明 |
| --- | --- | --- |
| `RabbitMqOptions` | `options`（回调配置后） | 单例，工厂闭包共享同一实例 |
| `IRabbitMqConnection` | `RabbitMqConnection` | 单例 |
| `IRabbitMqChannelPool` keyed `"publisher"` | `RabbitMqChannelPool(conn, ChannelPoolSize)` | 发布者池 |
| `IRabbitMqChannelPool` keyed `"consumer"` | `RabbitMqChannelPool(conn, ConsumerChannelPoolSize)` | 消费者池 |
| `IRabbitMqPublisher` | `RabbitMqPublisher`（工厂注入 keyed `"publisher"` 池） | 单例 |
| `IRabbitMqOutboxStore` | `InMemoryRabbitMqOutboxStore`（`TryAddSingleton`，可替换） | |
| `IRabbitMqOutbox` | `RabbitMqOutbox` | 单例 |
| HostedService | `RabbitMqOutboxDispatcher` | 后台调度 |

消费者子类（如 `DemoConsumer`）在调用方注册，其构造函数用 `[FromKeyedServices("consumer")] IRabbitMqChannelPool` 注入消费者池。非 keyed 的 `IRabbitMqChannelPool` 不再注册——发布者经工厂、消费者经 keyed 特性分别解析，无孤儿消费者。

## 5. 连接 `RabbitMqConnection`

`RabbitMqConnection.cs`，实现 `IRabbitMqConnection : IAsyncDisposable`。

- 构造时创建 `ConnectionFactory`，透传 options 的恢复参数。
- `GetConnectionAsync()` 使用**双重检查锁定**：
  1. 快速路径：`_connection is { IsOpen: true }` 直接返回（无锁）。
  2. 否则 `await _connectionLock.WaitAsync()`（`SemaphoreSlim(1,1)`）。
  3. 二次检查；释放旧连接后 `CreateConnectionAsync()`。
- `DisposeAsync` 释放连接与信号量。

## 6. 通道池 `RabbitMqChannelPool`

`internal sealed`，实现 `IRabbitMqChannelPool` + 内部 `IRabbitMqChannelPoolLease`。构造 `(IRabbitMqConnection connection, int poolSize)`——池大小由调用方传入，区分发布者/消费者池。

- `_pool`：`ConcurrentQueue<(IChannel Channel, ChannelConfirmTracker Tracker)>`。
- `_gate`：`SemaphoreSlim(poolSize)` 控制最大并发通道数。
- `RentAsync(ct)`：
  1. `ObjectDisposedException.ThrowIf` 检查。
  2. `await _gate.WaitAsync(ct)`。
  3. 从队列 `TryDequeue`，跳过已关闭通道（`DisposeAsync` tracker + channel）；命中 `IsOpen` 即返回租约（携带其 tracker）。
  4. 队列空则 `GetConnectionAsync()` + `CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct)` 新建，绑定 `ChannelConfirmTracker`。
  5. 异常时 `catch` 中 `_gate.Release()` 再 `throw`。
- `ReturnAsync(channel, tracker)`：池已释放（`_disposed`）或通道已关闭则 `DisposeAsync` tracker + channel；已释放时直接 return（不触碰已释放信号量，避免关停时 `ObjectDisposedException`）；否则入队。最后 `_gate.Release()`。

### 租约 `PooledChannelLease`

`readonly struct`，实现 `IAsyncDisposable`：

- `Channel` 属性暴露 `IChannel`；`Tracker`（`internal ChannelConfirmTracker?`）暴露该通道的确认追踪器，供发布者使用。
- `DisposeAsync()` → `_lease.ReturnAsync(Channel, Tracker!)`——归还时**携带 tracker**，保证同一 tracker 随通道跨租约复用，不重复订阅事件。

## 7. 发布确认追踪器 `ChannelConfirmTracker`

`internal sealed`，每个池化通道绑定一个。

- 订阅 `IChannel.BasicAcksAsync` / `BasicNacksAsync`，维护 `ConcurrentDictionary<ulong, TaskCompletionSource<PublishConfirmResult>>`。
- `Register(seq)`：注册 TCS（`RunContinuationsAsynchronously`）。
- `WaitAsync(seq, timeout, ct)`：序列号不存在返回 `Confirmed`；否则 `await tcs.Task.WaitAsync(timeout, ct)`，`TimeoutException`→`TimedOut`，取消向上抛。
- `Remove(seq)`：移除条目。
- ack 事件回填 `Confirmed`（处理 `Multiple` 批量，`kvp.Key <= DeliveryTag`）；nack 事件回填 `Nacked`。
- `DisposeAsync`：取消订阅、对残留 TCS `TrySetException(ObjectDisposedException)`、清空。

`PublishConfirmResult` 枚举：`Confirmed` / `Nacked` / `TimedOut`——区分 broker 拒绝与超时。

> 说明：`CreateChannelOptions.PublisherConfirmationsEnabled` 开启 publisher confirm（broker 会回 ack/nack）；`PublisherConfirmationTrackingEnabled` 额外让库为每条消息加发布序列号头以便 `basic.return`（mandatory 不可路由）关联。ack/nack 关联仍由本追踪器手动完成（事件 + `GetNextPublishSequenceNumberAsync`）。通道由池独占租约（`RentAsync` 出队），单通道上无并发 publish 序列号冲突。

## 8. 发布者 `RabbitMqPublisher`

`internal sealed`，实现 `IRabbitMqPublisher`。构造 `(IRabbitMqChannelPool channelPool, RabbitMqOptions options)`。

签名（`confirm`/`delayMs` 均带默认值）：

```csharp
ValueTask PublishAsync<T>(string exchange, string routingKey, T message,
    Action<IBasicProperties>? props = null, bool confirm = true,
    int? delayMs = null, CancellationToken cancellationToken = default);

ValueTask PublishRawAsync(string exchange, string routingKey, ReadOnlyMemory<byte> body,
    IDictionary<string,object?>? headers = null, Action<IBasicProperties>? props = null,
    bool confirm = true, int? delayMs = null, CancellationToken cancellationToken = default);
```

- `PublishAsync<T>`：`message` 为 `string` 走 UTF-8 编码（兑现接口契约，使字符串消费者收到原文）；否则 `JsonSerializer.SerializeToUtf8Bytes`。再委托 `PublishRawAsync`。
- `PublishRawAsync`：
  1. `await using var lease = await _channelPool.RentAsync(ct)`。
  2. 构造 headers 字典（拷贝或新建），`delayMs` 有值则设 `headers["x-delay"]`。
  3. 构造 `BasicProperties { Persistent = true, Headers = headersDict }`，应用 `props` 委托。
  4. 若 `confirm && tracker != null`：`seq = await channel.GetNextPublishSequenceNumberAsync(ct)`，`tcs = tracker.Register(seq)`。
  5. `BasicPublishAsync(exchange, routingKey, mandatory:false, basicProperties, body, ct)`。
  6. 若 confirm：`await tracker.WaitAsync(seq, PublisherConfirmTimeout, ct)`，`Nacked`→抛 `RabbitMqPublishNackedException(seq)`，`TimedOut`→抛 `TimeoutException`。
  7. `finally`：`tracker.Remove(seq)`。`await using` 退出时归还通道。
- `confirm=false`：不注册 TCS、不等待；broker 仍会 ack，追踪器找不到序列号即跳过。

`RabbitMqPublishNackedException`：broker 通过 `basic.nack` 明确拒绝时抛出，携带 `PublishSequenceNumber`。与超时（`TimeoutException`）区分，便于调用方重试/告警分别处理。

## 9. 消费者 `RabbitMqConsumerBase<T>`

抽象类，`where T : class`，实现 `IRabbitMqConsumer, IAsyncDisposable`。构造 `(IRabbitMqChannelPool channelPool, RabbitMqOptions options)`——由子类经 `[FromKeyedServices("consumer")]` 注入消费者池。

子类实现：`Queue` / `Exchange` / `RoutingKey`（抽象属性）、`HandleAsync(T, ct)`（抽象）。`protected virtual string ExchangeType => "direct"` 可覆盖。

`StartAsync(ct)` 流程：

1. 双重启动保护：已启动则抛 `InvalidOperationException`。
2. 从消费者池 `RentAsync` 租用通道。
3. `ExchangeDeclareAsync(Exchange, ExchangeType, durable:true, autoDelete:false)`。
4. 若 `_options.EnableDeadLetter`：构造主队列 args（`x-dead-letter-exchange`、`x-dead-letter-routing-key`、可选 `x-message-ttl`），声明 DLX（`RabbitMQ.Client.ExchangeType.Direct`）、DLQ 并绑定 DLQ→DLX→DeadLetterRoutingKey。
5. `QueueDeclareAsync(Queue, durable:true, exclusive:false, autoDelete:false, arguments:args)`——主队列**先声明再绑定**（修正原仅 bind 不 declare 的缺陷）。
6. `QueueBindAsync(Queue, Exchange, RoutingKey)`。
7. `BasicQosAsync(0, PrefetchCount, false)`。
8. `AsyncEventingBasicConsumer`，`ReceivedAsync`：反序列化 → `HandleAsync` → 成功 `BasicAckAsync`；异常 `BasicNackAsync(requeue:true)`。handler 使用启动时捕获的本地通道引用，避免 `Dispose` 后空引用。
9. `BasicConsumeAsync(autoAck:false)`，**捕获返回的 consumerTag**。

`DisposeAsync`：先 `BasicCancelAsync(consumerTag)` 停止消费（使通道无在途投递、归还后可复用），再 `lease.DisposeAsync()` 归还。通道关闭等异常忽略。

> 注意：`requeue:true` 会使毒消息（如反序列化失败）在同队列无限重入，DLX 仅在 TTL 过期/队列长度超限时触发，不捕获 nack 毒消息。生产应考虑 `requeue:false` 或重试计数头转向 DLX（待后续设计）。

## 10. Outbox 模式

### 10.1 消息实体 `RabbitMqOutboxMessage`

`public sealed`：`Id`（Guid init）、`Exchange`/`RoutingKey`（required init）、`Body`（byte[] init）、`Headers`（`Dictionary<string,object?>` init）、`CreatedAt`（init）、`PublishedAt`（set?）、`RetryCount`（set）、`LastError`（set?）、`NextAttemptAt`（DateTimeOffset? set）、`DeadLettered`（bool set）。

### 10.2 入箱 `RabbitMqOutbox`

`internal sealed`，实现 `IRabbitMqOutbox`。`EnqueueAsync<T>`：构造 `BasicProperties` 应用 `props`、`JsonSerializer.SerializeToUtf8Bytes` 序列化、构造 `RabbitMqOutboxMessage`（Headers 取自 properties.Headers）、`_store.AddAsync`。

### 10.3 存储抽象与实现

`IRabbitMqOutboxStore`：

- `AddAsync(message, ct)`
- `GetPendingAsync(DateTimeOffset now, int takeCount, ct)`：返回 `PublishedAt is null && !DeadLettered && (NextAttemptAt is null || NextAttemptAt <= now)` 的消息，按 `CreatedAt` 升序取前 N。
- `MarkAsPublishedAsync(messageId, publishedAt, ct)`
- `MarkAsFailedAsync(messageId, error, nextAttemptAt, ct)`：`RetryCount += 1`、`LastError`、`NextAttemptAt`。
- `MarkAsDeadLetterAsync(messageId, ct)`：`DeadLettered = true`。

`InMemoryRabbitMqOutboxStore`（`internal sealed`，`ConcurrentDictionary<Guid, RabbitMqOutboxMessage>`）实现上述。`MarkAs*` 在 messageId 不存在时静默跳过。

> 注意：内存存储进程重启即丢失，仅适用开发/测试。生产应替换 `IRabbitMqOutboxStore`（`TryAddSingleton` 允许覆盖，如数据库存储）。

### 10.4 后台调度器 `RabbitMqOutboxDispatcher`

`internal sealed : BackgroundService`。`ExecuteAsync(stoppingToken)` 循环：

1. `now = DateTimeOffset.UtcNow`，`GetPendingAsync(now, OutboxBatchSize, ct)`。
2. 逐条（串行 `foreach` + `await`）：
   - `RetryCount >= MaxRetryCount` → `DeadLetterAsync`，continue。
   - `PublishRawAsync(Exchange, RoutingKey, Body, Headers, ct)` → 成功 `MarkAsPublishedAsync`。
   - 失败：`newRetry = RetryCount + 1`；`newRetry >= MaxRetryCount` → `DeadLetterAsync`；否则指数退避 `backoff = Min(RetryMaxDelay, RetryBaseDelay × 2^min(newRetry,30))`，`nextAttempt = UtcNow + backoff`，`MarkAsFailedAsync(id, error, nextAttempt, ct)`。
3. 外层异常 `LogError`。
4. `Task.Delay(OutboxDispatchInterval, ct)`；`TaskCanceledException`（停止时）退出。

`DeadLetterAsync(message, reason, ct)`：`MarkAsDeadLetterAsync(id, ct)` + 日志；尽力把原 `Body`/`Headers` 发到 `DeadLetterExchange`/`DeadLetterRoutingKey`（默认 `confirm=true`），发送失败仅记日志——消息保持 `DeadLettered=true` 不再重试（避免死信死循环，但 DLX 不可达时该消息既不重试也未进 DLX，存在丢失风险）。

> 重试计数：调度器读取 `GetPendingAsync` 快照的 `RetryCount` 判阈值、计算 `newRetry`，由 `MarkAsFailedAsync` 内部自增——每次失败恰好一次自增，无双计。

## 11. 调用方使用方式（见 `Test3`）

```
AddUnifiedRabbitMq(opt => { ... })        # 注册（keyed 双池）
   ↓
IRabbitMqPublisher.PublishAsync(...)      # 实时直发（默认 confirm=true）
IRabbitMqOutbox.EnqueueAsync(...)         # 入箱，后台异步投递
IRabbitMqConsumer.StartAsync()           # 启动消费者；用完 DisposeAsync 停止
```

详见 [05-Test-测试项目.md](./05-Test-测试项目.md)。

## 12. 已知限制与后续事项

- **消费者毒消息循环**：`RabbitMqConsumerBase` nack 用 `requeue:true`，DLX 仅捕获 TTL/长度超限，不捕获 nack 毒消息。需 `requeue:false` 或重试计数头转向 DLX。
- **`x-message-ttl` 精度**：`DefaultMessageTTL` 转 `int` 毫秒，超过约 24.8 天溢出。
- **Outbox 死信丢失风险**：DLX 不可达时死信消息标记 `DeadLettered` 但未进 DLX。生产存储实现可强化。
- **Outbox 串行投递**：批量内逐条串行，慢消息阻塞后续。可改并发投递。
- **内存 Outbox 存储**：进程重启即丢，仅开发/测试。
