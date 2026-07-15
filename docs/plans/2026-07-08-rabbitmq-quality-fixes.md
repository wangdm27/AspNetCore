# RabbitMq 库质量优化改动报告（2026-07-08）

## 背景

基于 `code-reviewer` 对 `AspNetCore.RabbitMq/` 的全量基线审查结果：
**0 critical / 3 high / 7 medium / 8 low**，另记录 4 个"已核查非问题"。
本报告给出分阶段优化方案。审查命中两条历史坑（traceparent 断裂、Ack/Nack）。

## 目标

- 修复 3 个 high：outbox traceparent 断裂、消费者毒消息死循环、outbox 死信静默丢失。
- 偿还关键 medium：Options 校验、props 丢失、headers 覆盖、消费者生命周期、confirm 开销、InMemory 默认注册告警。
- 清理 low：XML doc、噪音注释、TTL 溢出等。
- 每项配套回归测试，保持现有 189 测试基线全绿并增量覆盖。

## 原则

- 不破坏公共面板签名（`IRabbitMqOutbox`/`IRabbitMqPublisher` 等），必要时仅扩展。
- 改动分层、可独立提交，便于回滚。
- 测试同步，每提交 `dotnet test AspNetCore.slnx` 全绿。

---

## Phase 1 — High（必须，合并前）

### H1. Outbox 路径 traceparent 全链路贯通

**问题**：`RabbitMqOutbox.EnqueueAsync` 未捕获请求上下文 `Activity.Current`；dispatcher 在 BackgroundService 上下文发布时 `Activity.Current` 为 null，`PublishRawAsync` 的 `Inject` 写不出 traceparent。直发路径正常，**仅 outbox 路径断**，Api 链路与消费链路 TraceId 接不上。

**改动**：
1. `RabbitMqOutbox.EnqueueAsync`（[RabbitMqOutbox.cs:49](../../AspNetCore.RabbitMq/RabbitMqOutbox.cs#L49)）：构建 headers 后，在请求上下文调 `RabbitMqTracing.Inject(headers)`，把入队时 traceparent 持久化进 `outboxMessage.Headers`。
2. `RabbitMqPublisher.PublishRawAsync`（[RabbitMqPublisher.cs:91](../../AspNetCore.RabbitMq/RabbitMqPublisher.cs#L91)）：`Inject` 改为"仅当 headers 不含 traceparent 才注入"，避免覆盖 outbox 已携带的父链路；直发路径仍注入当前 Activity。
3. 与 M6 合并：props 回调后"合并"而非"替换" headersDict。

**测试**：
- `RabbitMqTracingTests` 增 `Inject_WhenHeadersAlreadyContainTraceparent_DoesNotOverwrite`。
- 新 `RabbitMqOutboxTests`：`EnqueueAsync_WithActiveActivity_CapturesTraceparentIntoHeaders`。
- 端到端（Integration，标 `Skip`）：直发与 outbox 两路径 TraceId 一致。

### H2. 消费者毒消息重试上限 + DLX 兜底

**问题**：`RabbitMqConsumerBase.ReceivedAsync`（[RabbitMqConsumerBase.cs:123](../../AspNetCore.RabbitMq/RabbitMqConsumerBase.cs#L123)）`catch` 无差别 `BasicNackAsync(requeue:true)`；RabbitMQ DLX 只在 `requeue:false` 触发，配了 `EnableDeadLetter` 也无效；毒消息 tight loop 打满 CPU、饿死其它消息。

**改动**（`RabbitMqConsumerBase`）：
1. `RabbitMqOptions` 新增 `ConsumerMaxRetryCount`（默认 5）。
2. 维护 `ConcurrentDictionary<string,int> _retryCounts`，key = `BasicProperties.MessageId`（空则 body SHA256）。
3. `ReceivedAsync` 重构：
   - `catch (OperationCanceledException)`：`requeue:true`（保消息），不计重试。
   - `catch (Exception)`：计数 +1；`< 上限` → `requeue:true`；`>= 上限` → `requeue:false`（DLX 接管或丢弃）+ 移除计数 + warn。
   - ack 成功：移除计数。
4. 内存 best-effort 计数，进程重启重置；真正的指数退避需 retry 队列（x-delayed-message 或 TTL+DLX），列为后续 follow-up，不在本次。

**测试**（新 `RabbitMqConsumerBaseTests`，消费者为 public abstract，mock `IRabbitMqChannelPool`/`IChannel`）：
- 毒消息达上限后 `requeue:false`。
- 未达上限 `requeue:true` 且计数递增。
- OCE 路径 `requeue:true` 不计数。
- ack 后计数清除。

### H3. Outbox 死信不再静默丢失

**问题**：`RabbitMqOutboxDispatcher.DeadLetterAsync`（[RabbitMqOutboxDispatcher.cs:156](../../AspNetCore.RabbitMq/RabbitMqOutboxDispatcher.cs#L156)）无条件往 `DeadLetterExchange`（默认空串）发 + `mandatory:false`，broker 静默丢；`EnableDeadLetter` 标志未参与。

**改动**（`DeadLetterAsync`）：
1. `MarkAsDeadLetterAsync` + warn 后，若 `string.IsNullOrEmpty(_options.DeadLetterExchange)`：不再发布，记 warn "no DLX configured, message held as DeadLettered"，return。
2. 非空才尝试发布到 DLX。
3. 与 M4 联动：Options 校验"启用 `EnableDeadLetter` 时 `DeadLetterExchange`/`Queue`/`RoutingKey` 必须非空"，从源头堵误配。

**测试**（`RabbitMqOutboxDispatcherTests` 增）：
- `DeadLetterAsync_WithEmptyDeadLetterExchange_DoesNotPublishAndHoldsAsDeadLettered`。
- 既有 `PublishFailsRetryExhausted_MarksAsDeadLetterAndPublishesToDlx`（DLX 非空）保持绿。

---

## Phase 2 — Medium（应做）

### M4. `RabbitMqOptions.Validate()`

1. `RabbitMqOptions` 增 `public void Validate()`（非法抛 `ArgumentException`）。
2. `AddUnifiedRabbitMq` 在 `configure(options)` 后调 `options.Validate()`；顺带 `ArgumentNullException.ThrowIfNull(configure)`（L18）。
3. 规则：`ChannelPoolSize>=1`、`ConsumerChannelPoolSize>=1`、`MaxRetryCount>=1`、`ConsumerMaxRetryCount>=1`、`RetryBaseDelay>=Zero`、`RetryMaxDelay>Zero`、`PublisherConfirmTimeout>Zero`；`EnableDeadLetter` 时三个死信项非空；`DefaultMessageTTL` 设了须 `>Zero` 且 `<=24.8 天`（配合 L17）。

**测试**：`RabbitMqOptionsTests` 增 Validate 各分支（合法通过 + 各非法抛）。

### M5. Outbox 持久化 BasicProperties 关键字段

**问题**：`EnqueueAsync` 只拷 `properties.Headers`，`ContentType`/`CorrelationId`/`MessageId`/`Persistent` 全丢。

1. `RabbitMqOutboxMessage` 增 `ContentType`/`CorrelationId`/`MessageId`/`Persistent`（可空/默认）。
2. `EnqueueAsync` 从 props 构造的 properties 拷这些字段进 outboxMessage。
3. dispatcher `PublishRawAsync` 调用传 props 回调重建这些字段。
4. InMemoryStore 无需改；DB-backed store 实现需扩展 schema，文档注明。

**测试**：`RabbitMqOutboxMessageTests` + `RabbitMqOutboxTests` 覆盖字段往返。

### M6. `PublishRawAsync` headers 合并

**改动**（与 H1 合并）：props 回调后，对 `properties.Headers` 与 `headersDict` 做"调用方优先、补缺"合并；traceparent 仅在缺失时注入。

**测试**：`RabbitMqPublisherTests`（无则新建，mock 通道池）覆盖 props 设 Headers 时入参 headers/`x-delay` 不丢。

### M7. 消费者 per-consumer 取消令牌

1. 持有 `CancellationTokenSource _cts`；`StartAsync` 用 `_cts.Token` 传给 `HandleAsync`，与 stoppingToken 解耦或 Linked。
2. `DisposeAsync`：先 `_cts.Cancel()` 停在途，再 BasicCancel，再归还。
（与 H2/M8 一并在消费者重构里做。）

### M8. 消费者 Dispose 排空在途回调

1. 引入 in-flight 计数（`SemaphoreSlim` 或 int + lock）。
2. `ReceivedAsync` 进入 +1、退出 -1。
3. `DisposeAsync`：BasicCancel 后等 in-flight 归零（带超时，如 5s），再归还 lease。

### M9. 消费者通道池关闭 publisher confirms

1. `RabbitMqChannelPool` 构造增 `bool enableConfirms`。
2. `RentAsync`：`enableConfirms=false` 时 `CreateChannelOptions(false,false)`，tracker=null。
3. `PooledChannelLease.Tracker` 已 nullable；`DisposeAsync`/`ReturnAsync` null-check tracker；去掉 `Tracker!`（L14）。
4. `IRabbitMqChannelPoolLease.ReturnAsync` tracker 参数改 nullable。
5. 装配：consumer 池 `enableConfirms:false`，publisher 池 `true`。

**测试**：`ChannelConfirmTrackerTests` 不变；新增/补 `RabbitMqChannelPoolTests` 验证两池建通道差异（mock `IConnection`/`IChannel`）。

### M10. InMemoryOutboxStore 生产告警

`RabbitMqOutboxDispatcher` 构造里 `if (_store is InMemoryRabbitMqOutboxStore) _logger.LogWarning("InMemory outbox store in use; not durable. Replace IRabbitMqOutboxStore for production.")`。

---

## Phase 3 — Low（顺手清）

- **L11**：`IRabbitMqConnection`/`RabbitMqConnection` public 补 XML doc。
- **L12**：`IRabbitMqOutbox` 的 `<exception>` doc 移到方法级；M4 校验 exchange/routingKey 后让 doc 与实现一致。
- **L13**：清 `RabbitMqOutbox`/Dispatcher/OutboxMessage/InMemoryStore 噪音注释（`// 序列化消息为JSON字节` 等），对齐 `RabbitMqTracing` 风格。
- **L17**：`x-message-ttl` 封顶 `int.MaxValue`；TTL 与 `EnableDeadLetter` 解耦（允许仅 TTL 不要 DLX）。
- **L18**：与 M4 一并 `ArgumentNullException.ThrowIfNull(configure)`。
- **L15/L16**：Dispose/TOCTOU 经核查 DI 顺序下安全，仅加注释说明前提，不改代码。

---

## Phase 4 — 架构 follow-up（可选，单独评估）

- 风险项：`RabbitMq.csproj` 反向依赖 `AspNetCore.Events`，只用 RabbitMq 的用户被强拉 Events 契约。建议拆 `AspNetCore.RabbitMq.Events` 包放 `RabbitMqEventBus`，或移至 Events 项目。涉及 csproj/命名空间/装配 API 调整，单独开 plan，不在本次。

---

## 提交策略

- Phase 1 一个提交（high 修复 + 测试）。
- Phase 2 拆 2-3 个提交（Options 校验 / 消费者重构 / 通道池）。
- Phase 3 一个提交（清理）。
- 每提交 `dotnet test AspNetCore.slnx` 全绿。
- 完成后用 `code-reviewer` "验收"模式复跑。

## 不在范围

- DB-backed `IRabbitMqOutboxStore` 实现（仅保留接口与告警）。
- 消费者指数退避 retry 队列模式。
- Phase 4 Events 解耦。
