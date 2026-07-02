# RabbitMq 库重构设计

> 日期：2026-06-29
> 分支：`codex/refactor-rabbitmq-library-for-stability-and-performance`
> 库：`AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`（net10.0，RabbitMQ.Client 7.2.0）
> 状态：当前无法编译（5 个语法错误，均在 `IRabbitMqPublisher.cs`）

## 1. 背景与目标

`AspNetCore.RabbitMq` 库处于半成品状态：接口与实现签名不一致、`RabbitMqOptions` 缺失被引用的死信属性、Outbox 无重试上限。本次重构目标：

1. **修复编译阻断**（5 个 `IRabbitMqPublisher.cs` 语法错误）。
2. **实现可靠性特性**：发布确认（confirm）、延迟投递（delayMs）、死信队列（DLX）、Outbox 重试上限 + 指数退避 + 死信兜底。
3. **双通道池隔离**：发布者池（短生命周期租约）与消费者池（长生命周期租约），同一池类型通过 DI keyed services 注册双实例，互不饿死。

## 2. 约束

- 目标框架 `net10.0`，`ImplicitUsings` / `Nullable` 均开启。
- RabbitMQ.Client **7.2.0**：confirm 模式不再用 `ConfirmSelectAsync`，而是在 `IConnection.CreateChannelAsync(CreateChannelOptions, ct)` 时通过 `CreateChannelOptions` 开启（`PublisherConfirmationsEnabled` + `PublisherConfirmationTrackingEnabled`）。事件为 `BasicAcksAsync` / `BasicNacksAsync`，序号 `IChannel.NextPublishSeqNo`。
- 延迟投递依赖 `rabbitmq_delayed_message_exchange` broker 插件：交换机类型 `x-delayed-message` + `x-delayed-type` 参数，消息设 `headers["x-delay"]`。未安装插件时延迟投递失败。
- 库本身为 classlib，无测试项目；验证依赖 `AspNetCore.Test3` 端到端运行 + `dotnet build` 编译通过。
- docs 目录当前 untracked，本次不提交（按用户要求）。

## 3. 架构

```
RabbitMqConnection（单连接，双重检查锁，自动恢复）
        │
        ├── publisherPool  IRabbitMqChannelPool  key="publisher"  ChannelPoolSize
        │     └─ channel 创建时 PublisherConfirmationsEnabled=true
        │        维护 ConcurrentDictionary<ulong, TaskCompletionSource<bool>>
        │        BasicAcksAsync → 回填 true / BasicNacksAsync → 回填 false
        │        confirm=true 时 publish 后 await TCS + 超时；confirm=false 不等
        │
        └── consumerPool  IRabbitMqChannelPool  key="consumer"  ConsumerChannelPoolSize
              └─ 消费者 StartAsync 租，持至 StopAsync/Dispose 归还
```

confirm 模式为 channel 级开关。池在创建 channel 时统一开启 confirm 模式（`CreateChannelOptions`），`confirm` 参数仅控制 publish 之后是否 await broker ack。`confirm=true`：注册 `NextPublishSeqNo` → TCS，发布后 await + 超时，finally 移除条目。`confirm=false`：直接返回不等。

## 4. 组件改动

### 4.1 `RabbitMqOptions` 新增属性

| 属性 | 类型 | 默认值 | 用途 |
| --- | --- | --- | --- |
| `EnableDeadLetter` | `bool` | `false` | 消费者是否启用 DLX |
| `DeadLetterExchange` | `string` | `""` | 死信交换机名 |
| `DeadLetterRoutingKey` | `string` | `""` | 死信路由键（修正原 bug：原代码误用 `DeadLetterQueue` 作路由键） |
| `DeadLetterQueue` | `string` | `""` | 死信队列名 |
| `DefaultMessageTTL` | `TimeSpan?` | `null` | 主队列 `x-message-ttl`，null 不设 |
| `MaxRetryCount` | `int` | `5` | Outbox 重试上限 |
| `RetryBaseDelay` | `TimeSpan` | `5s` | 指数退避基数 |
| `RetryMaxDelay` | `TimeSpan` | `5min` | 退避封顶 |
| `ConsumerChannelPoolSize` | `int` | `16` | 消费者池上限 |
| `PublisherConfirmTimeout` | `TimeSpan` | `10s` | confirm await 超时 |

### 4.2 `IRabbitMqPublisher` + `RabbitMqPublisher`

统一签名（删除 `IRabbitMqPublisher.cs:57-59` 悬空参数，将 confirm/delayMs 合并入方法签名）：

```csharp
ValueTask PublishAsync<T>(
    string exchange,
    string routingKey,
    T message,
    Action<IBasicProperties>? props = null,
    bool confirm = true,
    int? delayMs = null,
    CancellationToken cancellationToken = default);

ValueTask PublishRawAsync(
    string exchange,
    string routingKey,
    ReadOnlyMemory<byte> body,
    IDictionary<string, object?>? headers = null,
    Action<IBasicProperties>? props = null,
    bool confirm = true,
    int? delayMs = null,
    CancellationToken cancellationToken = default);
```

- `T is string` → UTF-8 编码（兑现接口 XML 注释契约，使 `DemoConsumer<string>` 收到原文而非带引号 JSON）；其余类型 → `JsonSerializer.SerializeToUtf8Bytes`。
- `delayMs` 有值 → `headers["x-delay"] = delayMs.ToString()`。
- 删除 `RabbitMqPublisher.cs` 重复的 `using RabbitMQ.Client;`（行 1 与行 5）。
- 删除 `RabbitMqPublisher.cs:53` 对未定义 `props` 变量的引用。
- `PublishAsync` 实现：不再含 `confirm`/`delayMs` 签名偏离接口的旧版本。
- 实现注入 keyed `"publisher"` 池。
- 调用方 `AspNetCore.Test3/Program.cs` 现有调用 `PublishAsync(exchange, routingKey, message)` 与 `EnqueueAsync(exchange, routingKey, message)` 兼容新签名（confirm/delayMs 有默认值）。

### 4.3 `RabbitMqChannelPool`

- channel 创建：`CreateChannelOptions(PublisherConfirmationsEnabled: true, PublisherConfirmationTrackingEnabled: true)`。
- 新增 `_pending = ConcurrentDictionary<ulong, TaskCompletionSource<bool>>`。
- 接 `BasicAcksAsync`（按 deliveryTag 批量回填 true）/ `BasicNacksAsync`（回填 false）。7.x ack/nack 事件可能携带 multiple 标志，回填需处理区间。
- 暴露 `Task<bool> WaitForConfirmAsync(ulong seq, TimeSpan timeout, ct)`：从 `_pending` 取 TCS，await + 超时；finally 移除。
- channel 归还 / dispose 时清空该 channel 残留 TCS（回填 false 或取消）。

### 4.4 `RabbitMqConsumerBase<T>`

- 注入 keyed `"consumer"` 池。
- `StartAsync` 流程：租 channel → `ExchangeDeclareAsync(Exchange, type, durable, autoDelete:false)` → 若 `_options.EnableDeadLetter`：声明 DLX + DLQ + 绑定；主队列 args 带 `x-dead-letter-exchange`、`x-dead-letter-routing-key`、（若 `DefaultMessageTTL` 非 null）`x-message-ttl` → `QueueDeclareAsync(Queue, durable, exclusive:false, autoDelete:false, args)` → `QueueBindAsync(Queue, Exchange, RoutingKey)` → `BasicQosAsync(0, PrefetchCount, false)` → `BasicConsumeAsync(autoAck:false)`。
- 主队列声明顺序：先 declare 主队列（带 DLX args），再 bind。修正原 `RabbitMqConsumerBase.cs:104` 只 bind 不 declare 的缺陷。
- 实现 `IAsyncDisposable`：`StopAsync`/`Dispose` 归还 consumerPool 租约（`ReturnAsync`）。
- ack/nack 逻辑不变（成功 `BasicAckAsync`，异常 `BasicNackAsync(requeue:true)`）。
- 交换机 `type`：基类提供 `protected virtual string ExchangeType => ExchangeType.Direct;`，子类可覆盖（DemoConsumer 默认 Direct 无需覆盖）。

### 4.4.1 confirm 与 ack 追踪细节

池 channel 统一开启 confirm 模式，故每次 publish 都会产生 broker ack，与 `confirm` 参数无关。`confirm` 参数仅决定是否 await：

- `confirm=true`：publish 前取 `NextPublishSeqNo`，注册 `_pending[seq] = new TaskCompletionSource<bool>()`；publish 后 `await WaitForConfirmAsync(seq, PublisherConfirmTimeout, ct)`；finally 移除 `_pending[seq]`。
- `confirm=false`：不注册 TCS，不 await。broker 仍会 ack，`BasicAcksAsync` 回填时若 `_pending` 无该 seq 则跳过（无副作用）。

### 4.5 `RabbitMqOutboxMessage` 加字段

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `NextAttemptAt` | `DateTimeOffset?` | 下次允许重试时间，null 立即可重试 |
| `DeadLettered` | `bool` | 是否已转死信，`set` |

### 4.6 `IRabbitMqOutboxStore` 扩展

- `GetPendingAsync(DateTimeOffset now, int takeCount, ct)`：过滤 `RetryCount < MaxRetryCount && (NextAttemptAt == null || NextAttemptAt <= now) && !DeadLettered`，按 `CreatedAt` 升序取前 N。
- `MarkAsFailedAsync(messageId, error, nextAttemptAt, ct)`：`RetryCount += 1`、设 `LastError`、设 `NextAttemptAt`。
- `MarkAsDeadLetterAsync(messageId, ct)`：设 `DeadLettered = true`。
- 保留 `AddAsync`、`MarkAsPublishedAsync(messageId, publishedAt, ct)`。

> **注意**：`GetPendingAsync` 签名变更（加 `now` 参数）会破坏旧调用方。`RabbitMqOutboxDispatcher` 是唯一调用方，同步修改。`InMemoryRabbitMqOutboxStore` 同步实现新签名。

### 4.7 `RabbitMqOutboxDispatcher`

foreach 内逐条处理（保持串行，与现状一致）：

1. publish 成功 → `MarkAsPublishedAsync(Id, UtcNow, ct)`。
2. publish 失败：
   - 若 `RetryCount + 1 >= MaxRetryCount` → `MarkAsDeadLetterAsync(Id, ct)` + 调 `publisher.PublishRawAsync` 发到 `DeadLetterExchange`（headers 保留原消息 Headers + 标记来源）；死信发送失败仅记日志，消息保持 `DeadLettered=true` 不再重试。
   - 否则 → `MarkAsFailedAsync(Id, ex.Message, nextAttemptAt, ct)`，`nextAttemptAt = UtcNow + Min(RetryMaxDelay, RetryBaseDelay * 2^RetryCount)`。
3. 外层异常 `LogError`；`Task.Delay(OutboxDispatchInterval)`；`TaskCanceledException` 退出。

### 4.8 `ServiceCollectionExtensions`

- keyed 双池：
  - `AddKeyedSingleton<IRabbitMqChannelPool>("publisher", (sp) => new RabbitMqChannelPool(conn, options, "publisher"))`（用 `ChannelPoolSize`）。
  - `AddKeyedSingleton<IRabbitMqChannelPool>("consumer", (sp) => new RabbitMqChannelPool(conn, options, "consumer"))`（用 `ConsumerChannelPoolSize`）。
- `RabbitMqPublisher` 构造注 `[FromKeyedServices("publisher")] IRabbitMqChannelPool`。
- `RabbitMqConsumerBase<T>` 构造注 `[FromKeyedServices("consumer")] IRabbitMqChannelPool`。
- `RabbitMqChannelPool` 需按 keyed 的池大小初始化信号量——构造函数加 `poolSize` 参数，由两个工厂分别传 `ChannelPoolSize` / `ConsumerChannelPoolSize`。
- `IRabbitMqPublisher`、`IRabbitMqOutbox` 等仍 Singleton；`IRabbitMqOutboxStore` 仍 `TryAddSingleton`。

### 4.9 `AspNetCore.Test3`

- 删除 `AspNetCore.Test3/RabbitMqHostedService.cs`（孤儿：命名空间 `AspNetCore.RabbitMq` 却在 Test3 项目、未在 `Program.cs` 注册、`Program.cs` 已手动 `StartAsync`）。
- Test3 自身编译问题（`Program.cs` 发 `DemoMessage`，`DemoConsumer<string>` 期望 `string`，且 `DemoMessage` 类型未定义）属库外，列为验证项，需单独对齐 DemoConsumer 类型或改 Program.cs 发 string。不在本 spec 范围。

## 5. 错误处理

- confirm await 超时（超过 `PublisherConfirmTimeout`）→ 抛 `TimeoutException`，publish 视为失败。
- DLX 声明失败 → 消费启动失败，异常上抛到 `StartAsync` 调用方。
- Outbox 死信发送失败 → 记日志，消息 `DeadLettered=true` 不再重试（不无限阻塞调度循环）。
- 池满 → `RentAsync` 等待信号量（已有，不变）。
- channel disposed（连接恢复后）→ 池 `RentAsync` 已跳过关闭通道。

## 6. 范围外事项（本 spec 不处理）

- **Test3 类型对齐**：`Program.cs` 发 `DemoMessage` 与 `DemoConsumer<string>` 期望不一致、`DemoMessage` 类型未定义，导致 Test3 自身编译不过。属库外，需单独修，不在本 spec。

## 7. 设计决策已定

- 消费者交换机 `type`：基类虚属性 `ExchangeType => ExchangeType.Direct`，子类可覆盖。
- DLX 死信消息体：Outbox 转死信时保留原消息 `Body`，`Headers` 追加来源标记。

## 8. 验证

1. `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj` 编译通过、零错误。
2. `dotnet build` 全解决方案编译通过。
3. `AspNetCore.Test3` 端到端：直发 + Outbox 消息被消费（需先修 Test3 类型对齐）。
