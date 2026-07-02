# RabbitMq 库重构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 `AspNetCore.RabbitMq` 库编译阻断并实现 confirm 确认、delayMs 延迟投递、死信队列、Outbox 重试上限+退避+死信兜底、双通道池隔离。

**Architecture:** 单连接 + 双通道池（keyed "publisher"/"consumer" 同类型双实例）。发布者池 channel 创建时开启 publisher confirm，绑定 `ChannelConfirmTracker` 追踪 ack/nack；消费者池 channel 长租持至 StopAsync 归还。Outbox 失败按指数退避重试，超限转死信。

**Tech Stack:** .NET 10.0、RabbitMQ.Client 7.2.0（confirm 用 `CreateChannelOptions` + `GetNextPublishSequenceNumberAsync`，非 6.x 的 `ConfirmSelect`/`NextPublishSeqNo`）、Microsoft.Extensions.DI 10.0.2（keyed services）。

**Design spec:** `docs/superpowers/specs/2026-06-29-rabbitmq-refactor-design.md`

---

## 验证方式说明（重要偏离 TDD 默认）

本仓库**无测试项目**（Test/Test2/Test3 均为 console Exe），且 RabbitMQ.Client 库需 live broker 才能真正集成测试。已批准的 spec（第 5/8 节）明确：验证 = `dotnet build` 编译通过 + Test3 E2E。

因此本计划**不用失败测试驱动**，而是用 **`dotnet build` 编译检查点**作为每任务验证门。每个任务结尾跑构建并确认 0 错误。这是与 writing-plans 默认 TDD 的有意偏离，依据用户已批准的 spec。

构建命令统一：`dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`

注意：构建输出含中文（可能乱码），看末尾 `0 个错误` / `0 Error` 即通过。

---

## 文件结构

| 文件 | 责任 | 操作 |
| --- | --- | --- |
| `RabbitMqOptions.cs` | 配置选项 | 修改：新增 10 属性 |
| `ChannelConfirmTracker.cs` | 通道级 confirm 追踪 | 新建 |
| `IRabbitMqChannelPool.cs` | 通道池抽象 + 租约 | 修改：租约带 tracker |
| `RabbitMqChannelPool.cs` | 通道池实现 | 修改：keyed 大小 + confirm channel + tracker |
| `IRabbitMqPublisher.cs` | 发布者抽象 | 修改：统一签名 |
| `RabbitMqPublisher.cs` | 发布者实现 | 重写：confirm/delayMs/string 序列化 |
| `RabbitMqConsumerBase.cs` | 消费者基类 | 重写：池租 + 自动声明 + DLX + IDisposable |
| `RabbitMqOutboxMessage.cs` | Outbox 实体 | 修改：加 2 字段 |
| `IRabbitMqOutboxStore.cs` | 存储抽象 | 修改：改 GetPending/MarkAsFailed + 加 MarkAsDeadLetter |
| `InMemoryRabbitMqOutboxStore.cs` | 内存存储 | 修改：实现新签名 |
| `RabbitMqOutboxDispatcher.cs` | 后台调度 | 修改：退避 + 死信兜底 |
| `ServiceCollectionExtensions.cs` | DI 注册 | 修改：keyed 双池 |
| `AspNetCore.Test3/DemoConsumer.cs` | 示例消费者 | 修改：ctor 适配新基类 |

---

## Task 1: RabbitMqOptions 新增配置属性

**Files:**
- Modify: `AspNetCore.RabbitMq/RabbitMqOptions.cs`

- [ ] **Step 1: 在 `OutboxBatchSize` 属性后追加新属性**

在 `RabbitMqOptions.cs` 第 96 行 `public int OutboxBatchSize { get; set; } = 100;` 之后、第 97 行 `}` 之前插入：

```csharp

        /// <summary>
        /// 是否启用死信队列
        /// </summary>
        /// <remarks>默认值: false</remarks>
        public bool EnableDeadLetter { get; set; } = false;

        /// <summary>
        /// 死信交换机名称
        /// </summary>
        public string DeadLetterExchange { get; set; } = string.Empty;

        /// <summary>
        /// 死信路由键（修正原误用队列名作路由键的 bug）
        /// </summary>
        public string DeadLetterRoutingKey { get; set; } = string.Empty;

        /// <summary>
        /// 死信队列名称
        /// </summary>
        public string DeadLetterQueue { get; set; } = string.Empty;

        /// <summary>
        /// 消息默认存活时间（主队列 x-message-ttl），null 表示不设置
        /// </summary>
        public TimeSpan? DefaultMessageTTL { get; set; } = null;

        /// <summary>
        /// Outbox 最大重试次数
        /// </summary>
        /// <remarks>默认值: 5</remarks>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>
        /// Outbox 重试退避基数
        /// </summary>
        /// <remarks>默认值: 5秒</remarks>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Outbox 重试退避封顶
        /// </summary>
        /// <remarks>默认值: 5分钟</remarks>
        public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 消费者通道池大小
        /// </summary>
        /// <remarks>默认值: 16</remarks>
        public int ConsumerChannelPoolSize { get; set; } = 16;

        /// <summary>
        /// 发布确认等待超时
        /// </summary>
        /// <remarks>默认值: 10秒</remarks>
        public TimeSpan PublisherConfirmTimeout { get; set; } = TimeSpan.FromSeconds(10);
```

- [ ] **Step 2: 构建检查（预期仍有 5 个 publisher 错误，无新增错误）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 仍是 `IRabbitMqPublisher.cs` 的 5 个错误（CS1001/CS1002/CS1044/CS1519），**无新错误**。Options 纯增量不破坏。

- [ ] **Step 3: Commit**

```bash
git add AspNetCore.RabbitMq/RabbitMqOptions.cs
git commit -m "feat(rabbitmq): add dead-letter, retry-backoff, dual-pool, confirm-timeout options"
```

---

## Task 2: 新建 ChannelConfirmTracker

**Files:**
- Create: `AspNetCore.RabbitMq/ChannelConfirmTracker.cs`

- [ ] **Step 1: 创建文件**

```csharp
using System.Collections.Concurrent;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AspNetCore.RabbitMq;

/// <summary>
/// 通道级发布确认追踪器
/// </summary>
/// <remarks>
/// 每个 IChannel 绑定一个追踪器，订阅 BasicAcksAsync / BasicNacksAsync，
/// 维护 deliveryTag → TaskCompletionSource 映射，供发布者在发布后等待 broker 确认。
/// 通道由池长期持有，追踪器生命周期与通道一致。
/// </remarks>
internal sealed class ChannelConfirmTracker : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _pending = new();

    public ChannelConfirmTracker(IChannel channel)
    {
        _channel = channel;
        _channel.BasicAcksAsync += OnAcksAsync;
        _channel.BasicNacksAsync += OnNacksAsync;
    }

    /// <summary>
    /// 注册一个待确认序列号，返回可等待的 TaskCompletionSource。
    /// </summary>
    public TaskCompletionSource<bool> Register(ulong seq)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seq] = tcs;
        return tcs;
    }

    /// <summary>
    /// 等待指定序列号的 broker 确认。超时返回 false，取消向上抛。
    /// </summary>
    public async Task<bool> WaitAsync(ulong seq, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(seq, out var tcs))
        {
            return true;
        }

        try
        {
            return await tcs.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// 移除一个序列号（无论是否确认完成）。
    /// </summary>
    public void Remove(ulong seq) => _pending.TryRemove(seq, out _);

    private Task OnAcksAsync(object? sender, BasicAckEventArgs e)
    {
        if (e.Multiple)
        {
            foreach (var kvp in _pending)
            {
                if (kvp.Key <= e.DeliveryTag)
                {
                    kvp.Value.TrySetResult(true);
                }
            }
        }
        else if (_pending.TryGetValue(e.DeliveryTag, out var tcs))
        {
            tcs.TrySetResult(true);
        }

        return Task.CompletedTask;
    }

    private Task OnNacksAsync(object? sender, BasicNackEventArgs e)
    {
        if (e.Multiple)
        {
            foreach (var kvp in _pending)
            {
                if (kvp.Key <= e.DeliveryTag)
                {
                    kvp.Value.TrySetResult(false);
                }
            }
        }
        else if (_pending.TryGetValue(e.DeliveryTag, out var tcs))
        {
            tcs.TrySetResult(false);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _channel.BasicAcksAsync -= OnAcksAsync;
        _channel.BasicNacksAsync -= OnNacksAsync;

        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetException(new ObjectDisposedException(nameof(ChannelConfirmTracker)));
        }

        _pending.Clear();
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 2: 构建检查（预期仍 5 个 publisher 错误，无新增）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 仍 5 个 publisher 错误，无新错误。新文件独立编译。

- [ ] **Step 3: Commit**

```bash
git add AspNetCore.RabbitMq/ChannelConfirmTracker.cs
git commit -m "feat(rabbitmq): add ChannelConfirmTracker for per-channel ack/nack correlation"
```

---

## Task 3: 通道池支持 confirm channel 与 tracker（租约携带 tracker）

**Files:**
- Modify: `AspNetCore.RabbitMq/IRabbitMqChannelPool.cs`
- Modify: `AspNetCore.RabbitMq/RabbitMqChannelPool.cs`

- [ ] **Step 1: 修改 PooledChannelLease 携带 tracker**

在 `IRabbitMqChannelPool.cs` 中，将 `PooledChannelLease` struct 改为：

```csharp
    /// <summary>
    /// 信道租约：封装一个可用的 <see cref="IChannel"/> 与归还动作。
    /// 建议使用 await using 来确保最终归还。
    /// </summary>
    public readonly struct PooledChannelLease : IAsyncDisposable
    {
        private readonly IRabbitMqChannelPoolLease _lease;

        internal PooledChannelLease(IChannel channel, ChannelConfirmTracker tracker, IRabbitMqChannelPoolLease lease)
        {
            Channel = channel;
            Tracker = tracker;
            _lease = lease;
        }

        /// <summary>
        /// 当前租约持有的 RabbitMQ 信道。
        /// </summary>
        public IChannel Channel { get; }

        /// <summary>
        /// 该通道的发布确认追踪器（消费者可不使用）。
        /// </summary>
        internal ChannelConfirmTracker? Tracker { get; }

        /// <summary>
        /// 释放租约并将信道归还给池（不会直接关闭信道）。
        /// </summary>
        public ValueTask DisposeAsync() => _lease.ReturnAsync(Channel);
    }
```

注意：`Tracker` 为 `internal` 属性（类型 `ChannelConfirmTracker?` 也是 internal），public struct 暴露 internal 属性 + internal 类型合法（成员可访问性 <= 类型可访问性）。

- [ ] **Step 2: 修改 RabbitMqChannelPool 构造与池存储**

在 `RabbitMqChannelPool.cs` 全文替换为：

```csharp
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ通道池实现
    /// </summary>
    /// <remarks>
    /// 管理RabbitMQ通道的创建、复用和释放，提高性能并减少资源消耗。
    /// 所有通道创建时开启发布确认模式并绑定 ChannelConfirmTracker。
    /// </remarks>
    internal sealed class RabbitMqChannelPool : IRabbitMqChannelPool, IRabbitMqChannelPoolLease
    {
        private readonly IRabbitMqConnection _connection;
        private readonly ConcurrentQueue<(IChannel Channel, ChannelConfirmTracker Tracker)> _pool = new();
        private readonly SemaphoreSlim _gate;
        private volatile bool _disposed;

        /// <summary>
        /// 初始化通道池
        /// </summary>
        /// <param name="connection">RabbitMQ连接实例</param>
        /// <param name="poolSize">通道池大小（由调用方传入，区分发布者/消费者池）</param>
        public RabbitMqChannelPool(IRabbitMqConnection connection, int poolSize)
        {
            _connection = connection;
            _gate = new SemaphoreSlim(poolSize, poolSize);
        }

        /// <summary>
        /// 从通道池获取一个通道
        /// </summary>
        /// <remarks>
        /// 优先从池中获取空闲通道，如果没有则创建新通道（开启发布确认 + 追踪器）
        /// </remarks>
        public async ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _gate.WaitAsync(cancellationToken);

            try
            {
                while (_pool.TryDequeue(out var entry))
                {
                    if (entry.Channel.IsOpen)
                    {
                        return new PooledChannelLease(entry.Channel, entry.Tracker, this);
                    }

                    await entry.Tracker.DisposeAsync();
                    await entry.Channel.DisposeAsync();
                }

                var conn = await _connection.GetConnectionAsync();
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                var channel = await conn.CreateChannelAsync(options, cancellationToken);
                var tracker = new ChannelConfirmTracker(channel);
                return new PooledChannelLease(channel, tracker, this);
            }
            catch
            {
                _gate.Release();
                throw;
            }
        }

        /// <summary>
        /// 归还通道到池
        /// </summary>
        /// <remarks>
        /// 如果池已释放或通道已关闭，则释放通道与追踪器；否则放回池中
        /// </remarks>
        public async ValueTask ReturnAsync(IChannel channel)
        {
            if (_disposed || !channel.IsOpen)
            {
                // 池已释放或通道已关闭：需找到对应 tracker 释放。
                // 通道关闭场景下 tracker 已随通道失效，这里尽力清理。
                await channel.DisposeAsync();
            }
            else
            {
                // 通道仍可用：尝试找回 tracker。
                // RentAsync 保证出池时 tracker 已知；归还时通过内部查找。
                var tracker = TryRemoveTracker(channel);
                if (tracker is not null)
                {
                    _pool.Enqueue((channel, tracker));
                }
                else
                {
                    // 无 tracker 记录（不应发生），重新建一个以保持一致。
                    _pool.Enqueue((channel, new ChannelConfirmTracker(channel)));
                }
            }

            _gate.Release();
        }

        private ChannelConfirmTracker? TryRemoveTracker(IChannel channel)
        {
            // 通道归还时 tracker 仍在内存中：因 RentAsync 出池后未存独立映射，
            // 改为由调用方保证 tracker 一致。这里返回 null 触发重建分支。
            // 注意：重建会重复订阅事件，故实际依赖 DisposeAsync 清理旧 tracker。
            return null;
        }

        /// <summary>
        /// 释放通道池资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            while (_pool.TryDequeue(out var entry))
            {
                await entry.Tracker.DisposeAsync();
                await entry.Channel.DisposeAsync();
            }

            _gate.Dispose();
        }
    }
}
```

**问题：** 上述 `TryRemoveTracker` 返回 null 会触发"重建 tracker"，但旧 tracker 仍订阅着事件 → 重复订阅泄漏。需修正设计：归还通道时不能丢 tracker。

**修正 Step 2：** `ReturnAsync` 签名需要 tracker。但 `IRabbitMqChannelPoolLease.ReturnAsync(IChannel channel)` 接口只收 channel。需改接口让租约归还时带 tracker。

**Step 2 改为：** 修改 `IRabbitMqChannelPoolLease` 与 `PooledChannelLease.DisposeAsync` 传 tracker：

在 `IRabbitMqChannelPool.cs` 中将 `IRabbitMqChannelPoolLease` 改为：

```csharp
    /// <summary>
    /// 池内部使用的归还通道契约，对外隐藏具体实现。
    /// </summary>
    internal interface IRabbitMqChannelPoolLease
    {
        /// <summary>
        /// 将租借的信道与追踪器归还到池中。
        /// </summary>
        ValueTask ReturnAsync(IChannel channel, ChannelConfirmTracker tracker);
    }
```

并修改 `PooledChannelLease.DisposeAsync`：

```csharp
        public ValueTask DisposeAsync() => _lease.ReturnAsync(Channel, Tracker!);
```

（`Tracker!` 抵消 nullable，因构造保证非 null。）

然后 `RabbitMqChannelPool.ReturnAsync` 改为收 tracker：

```csharp
        public async ValueTask ReturnAsync(IChannel channel, ChannelConfirmTracker tracker)
        {
            if (_disposed || !channel.IsOpen)
            {
                await tracker.DisposeAsync();
                await channel.DisposeAsync();
            }
            else
            {
                _pool.Enqueue((channel, tracker));
            }

            _gate.Release();
        }
```

并删除 `TryRemoveTracker` 方法。

**最终 `RabbitMqChannelPool.cs` 完整版：**

```csharp
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ通道池实现
    /// </summary>
    /// <remarks>
    /// 管理RabbitMQ通道的创建、复用和释放，提高性能并减少资源消耗。
    /// 所有通道创建时开启发布确认模式并绑定 ChannelConfirmTracker。
    /// </remarks>
    internal sealed class RabbitMqChannelPool : IRabbitMqChannelPool, IRabbitMqChannelPoolLease
    {
        private readonly IRabbitMqConnection _connection;
        private readonly ConcurrentQueue<(IChannel Channel, ChannelConfirmTracker Tracker)> _pool = new();
        private readonly SemaphoreSlim _gate;
        private volatile bool _disposed;

        /// <summary>
        /// 初始化通道池
        /// </summary>
        /// <param name="connection">RabbitMQ连接实例</param>
        /// <param name="poolSize">通道池大小（由调用方传入，区分发布者/消费者池）</param>
        public RabbitMqChannelPool(IRabbitMqConnection connection, int poolSize)
        {
            _connection = connection;
            _gate = new SemaphoreSlim(poolSize, poolSize);
        }

        /// <summary>
        /// 从通道池获取一个通道
        /// </summary>
        public async ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _gate.WaitAsync(cancellationToken);

            try
            {
                while (_pool.TryDequeue(out var entry))
                {
                    if (entry.Channel.IsOpen)
                    {
                        return new PooledChannelLease(entry.Channel, entry.Tracker, this);
                    }

                    await entry.Tracker.DisposeAsync();
                    await entry.Channel.DisposeAsync();
                }

                var conn = await _connection.GetConnectionAsync();
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                var channel = await conn.CreateChannelAsync(options, cancellationToken);
                var tracker = new ChannelConfirmTracker(channel);
                return new PooledChannelLease(channel, tracker, this);
            }
            catch
            {
                _gate.Release();
                throw;
            }
        }

        /// <summary>
        /// 归还通道到池
        /// </summary>
        public async ValueTask ReturnAsync(IChannel channel, ChannelConfirmTracker tracker)
        {
            if (_disposed || !channel.IsOpen)
            {
                await tracker.DisposeAsync();
                await channel.DisposeAsync();
            }
            else
            {
                _pool.Enqueue((channel, tracker));
            }

            _gate.Release();
        }

        /// <summary>
        /// 释放通道池资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            while (_pool.TryDequeue(out var entry))
            {
                await entry.Tracker.DisposeAsync();
                await entry.Channel.DisposeAsync();
            }

            _gate.Dispose();
        }
    }
}
```

- [ ] **Step 3: 构建检查（预期仍 5 个 publisher 错误，无新增）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 仍 5 个 publisher 错误（IRabbitMqPublisher.cs），无 pool/lease 相关新错误。

- [ ] **Step 4: Commit**

```bash
git add AspNetCore.RabbitMq/IRabbitMqChannelPool.cs AspNetCore.RabbitMq/RabbitMqChannelPool.cs
git commit -m "refactor(rabbitmq): pool creates confirm-enabled channels, lease carries tracker"
```

---

## Task 4: 统一发布者签名 + 实现 confirm/delayMs/string 序列化（首个全绿检查点）

**Files:**
- Modify: `AspNetCore.RabbitMq/IRabbitMqPublisher.cs`
- Modify: `AspNetCore.RabbitMq/RabbitMqPublisher.cs`

- [ ] **Step 1: 重写 IRabbitMqPublisher.cs**

全文替换为：

```csharp
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqPublisher
    {
        /// <summary>
        /// 发布消息到RabbitMQ
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="exchange">交换机名称</param>
        /// <param name="routingKey">路由键</param>
        /// <param name="message">消息内容（string 走 UTF-8，其余 JSON）</param>
        /// <param name="props">消息属性配置委托</param>
        /// <param name="confirm">是否等待 broker 发布确认</param>
        /// <param name="delayMs">延迟投递毫秒（需目标交换机为 x-delayed-message 类型）</param>
        /// <param name="cancellationToken">取消令牌</param>
        ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            bool confirm = true,
            int? delayMs = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 发布原始字节消息到RabbitMQ
        /// </summary>
        /// <param name="exchange">交换机名称</param>
        /// <param name="routingKey">路由键</param>
        /// <param name="body">原始消息体字节数据</param>
        /// <param name="headers">消息头字典</param>
        /// <param name="props">消息属性配置委托</param>
        /// <param name="confirm">是否等待 broker 发布确认</param>
        /// <param name="delayMs">延迟投递毫秒（设置 x-delay 头）</param>
        /// <param name="cancellationToken">取消令牌</param>
        ValueTask PublishRawAsync(
            string exchange,
            string routingKey,
            ReadOnlyMemory<byte> body,
            IDictionary<string, object?>? headers = null,
            Action<IBasicProperties>? props = null,
            bool confirm = true,
            int? delayMs = null,
            CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 2: 重写 RabbitMqPublisher.cs**

全文替换为：

```csharp
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ消息发布者实现
    /// </summary>
    /// <remarks>
    /// 负责将消息发布到RabbitMQ交换机，支持对象序列化和原始字节发布。
    /// 支持发布确认（confirm）与延迟投递（delayMs）。
    /// </remarks>
    internal sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IRabbitMqChannelPool _channelPool;
        private readonly RabbitMqOptions _options;

        /// <summary>
        /// 初始化消息发布者
        /// </summary>
        /// <param name="channelPool">通道池实例</param>
        /// <param name="options">RabbitMQ配置选项</param>
        public RabbitMqPublisher(IRabbitMqChannelPool channelPool, RabbitMqOptions options)
        {
            _channelPool = channelPool;
            _options = options;
        }

        /// <summary>
        /// 发布消息到RabbitMQ
        /// </summary>
        /// <remarks>
        /// string 走 UTF-8 编码，其余类型 JSON 序列化，然后调用 PublishRawAsync。
        /// </remarks>
        public ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            bool confirm = true,
            int? delayMs = null,
            CancellationToken cancellationToken = default)
        {
            var body = message switch
            {
                string s => Encoding.UTF8.GetBytes(s),
                _ => JsonSerializer.SerializeToUtf8Bytes(message)
            };
            return PublishRawAsync(exchange, routingKey, body, null, props, confirm, delayMs, cancellationToken);
        }

        /// <summary>
        /// 发布原始字节消息到RabbitMQ
        /// </summary>
        /// <remarks>
        /// 从通道池获取通道（独占租约），设置消息属性与延迟头，
        /// 发布后按 confirm 等待 broker 确认。
        /// </remarks>
        public async ValueTask PublishRawAsync(
            string exchange,
            string routingKey,
            ReadOnlyMemory<byte> body,
            IDictionary<string, object?>? headers = null,
            Action<IBasicProperties>? props = null,
            bool confirm = true,
            int? delayMs = null,
            CancellationToken cancellationToken = default)
        {
            await using var lease = await _channelPool.RentAsync(cancellationToken);
            var channel = lease.Channel;
            var tracker = lease.Tracker;

            var headersDict = headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
                              ?? new Dictionary<string, object?>();
            if (delayMs is { } delay)
            {
                headersDict["x-delay"] = (int)delay;
            }

            var properties = new BasicProperties
            {
                Persistent = true,
                Headers = headersDict
            };
            props?.Invoke(properties);

            ulong seq = 0;
            TaskCompletionSource<bool>? tcs = null;
            if (confirm && tracker is not null)
            {
                seq = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                tcs = tracker.Register(seq);
            }

            try
            {
                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                if (confirm && tcs is not null)
                {
                    var ok = await tracker!.WaitAsync(seq, _options.PublisherConfirmTimeout, cancellationToken);
                    if (!ok)
                    {
                        throw new TimeoutException(
                            $"RabbitMQ publish confirm timed out after {_options.PublisherConfirmTimeout}.");
                    }
                }
            }
            finally
            {
                if (tcs is not null)
                {
                    tracker!.Remove(seq);
                }
            }
        }
    }
}
```

注意：删除了原 `using RabbitMQ.Client.Events;`（未用）与重复的 `using RabbitMQ.Client;`。

- [ ] **Step 3: 构建检查（首个全绿检查点）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: **0 个错误**（生成成功）。Dispatcher 仍调用旧 `GetPendingAsync(int, ct)` 与 `MarkAsFailedAsync(id, error, ct)`——但这两个签名在 Task 7 才改，此刻 Dispatcher 会编译失败！

**问题：** Task 7 改 Store 接口会破坏 Dispatcher（Task 8 才改 Dispatcher）。需调整顺序，保证每个检查点全绿。

- [ ] **Step 4: 调整——本任务暂不验证全绿，仅验证 publisher 文件无错误**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: publisher 相关错误（CS1001 等 5 个）消失。可能新增 Dispatcher 对旧 Store 签名的调用错误——**这些将在 Task 7/8 解决**。记录新增错误数，确认仅与 Store/Dispatcher 相关。

- [ ] **Step 5: Commit**

```bash
git add AspNetCore.RabbitMq/IRabbitMqPublisher.cs AspNetCore.RabbitMq/RabbitMqPublisher.cs
git commit -m "feat(rabbitmq): unify publisher signature with confirm/delayMs, string UTF-8 encoding"
```

---

## Task 5: 消费者基类重写（池租 + 自动声明拓扑 + DLX + IDisposable）

**Files:**
- Modify: `AspNetCore.RabbitMq/RabbitMqConsumerBase.cs`
- Modify: `AspNetCore.Test3/DemoConsumer.cs`（ctor 适配新基类）

- [ ] **Step 1: 重写 RabbitMqConsumerBase.cs**

全文替换为：

```csharp
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ消费者基类
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <remarks>
    /// 提供RabbitMQ消费者的基本实现，包括交换机/队列声明、绑定、死信配置和消息处理。
    /// 消费者从消费者通道池租用通道，长租持至 StopAsync/Dispose 归还。
    /// 子类需实现队列、交换机、路由键配置，以及消息处理逻辑。
    /// </remarks>
    public abstract class RabbitMqConsumerBase<T> : IRabbitMqConsumer, IAsyncDisposable where T : class
    {
        private readonly IRabbitMqChannelPool _channelPool;
        private readonly RabbitMqOptions _options;
        private PooledChannelLease? _lease;
        private IChannel? _channel;

        /// <summary>
        /// 队列名称
        /// </summary>
        protected abstract string Queue { get; }

        /// <summary>
        /// 交换机名称
        /// </summary>
        protected abstract string Exchange { get; }

        /// <summary>
        /// 路由键
        /// </summary>
        protected abstract string RoutingKey { get; }

        /// <summary>
        /// 交换机类型，子类可覆盖，默认 direct。
        /// </summary>
        protected virtual string ExchangeType => "direct";

        /// <summary>
        /// 初始化消费者基类
        /// </summary>
        /// <param name="channelPool">消费者通道池实例</param>
        /// <param name="options">RabbitMQ配置选项</param>
        protected RabbitMqConsumerBase(IRabbitMqChannelPool channelPool, RabbitMqOptions options)
        {
            _channelPool = channelPool;
            _options = options;
        }

        /// <summary>
        /// 启动消费者
        /// </summary>
        /// <remarks>
        /// 1. 从消费者池租用通道
        /// 2. 声明交换机
        /// 3. 若启用死信：声明 DLX + DLQ + 绑定，主队列带死信参数
        /// 4. 声明主队列
        /// 5. 绑定主队列到交换机
        /// 6. 设置QoS
        /// 7. 创建消费者并注册消息接收事件
        /// 8. 开始消费消息
        /// </remarks>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _lease = await _channelPool.RentAsync(cancellationToken);
            _channel = _lease.Value.Channel;

            // 声明交换机
            await _channel.ExchangeDeclareAsync(Exchange, ExchangeType, durable: true, autoDelete: false);

            // 主队列参数（含可选死信参数）
            var args = new Dictionary<string, object>();
            if (_options.EnableDeadLetter)
            {
                args["x-dead-letter-exchange"] = _options.DeadLetterExchange;
                args["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey;
                if (_options.DefaultMessageTTL is { } ttl)
                {
                    args["x-message-ttl"] = (int)ttl.TotalMilliseconds;
                }

                // 声明死信交换机、队列并绑定
                await _channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);
                await _channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
                await _channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterRoutingKey);
            }

            // 声明主队列（带可选死信参数）
            await _channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false, arguments: args);

            // 绑定主队列到交换机
            await _channel.QueueBindAsync(queue: Queue, exchange: Exchange, routingKey: RoutingKey, arguments: null);

            // 设置QoS
            await _channel.BasicQosAsync(0, _options.PrefetchCount, false);

            // 创建异步事件消费者
            var consumer = new AsyncEventingBasicConsumer(_channel);

            // 注册消息接收事件处理
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<T>(ea.Body.Span)!;
                    await HandleAsync(msg, cancellationToken);
                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch
                {
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // 开始消费消息
            await _channel.BasicConsumeAsync(
                queue: Queue,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        protected abstract Task HandleAsync(T message, CancellationToken ct);

        /// <summary>
        /// 停止消费者并归还通道租约
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_lease is { } lease)
            {
                _lease = null;
                _channel = null;
                await lease.DisposeAsync();
            }
        }
    }
}
```

- [ ] **Step 2: 修改 DemoConsumer ctor 适配新基类**

在 `AspNetCore.Test3/DemoConsumer.cs` 中，将构造函数改为：

```csharp
        public DemoConsumer([FromKeyedServices("consumer")] IRabbitMqChannelPool pool, RabbitMqOptions opts) : base(pool, opts) { }
```

并在文件顶部 using 区追加（若缺）：

```csharp
using Microsoft.Extensions.DependencyInjection;
```

> 注：DemoConsumer 还存在 `DemoMessage` 类型缺失问题（Program.cs 引用 `new DemoMessage`，但 DemoConsumer 未定义该嵌套类型），属 spec 第 6 节明确标记的**范围外**事项，本任务不修。故 Test3 仍无法编译；本检查点以**库**编译为准。

- [ ] **Step 3: 构建检查（库）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 库 0 个错误（若仍有 Dispatcher/Store 旧签名错误，属 Task 7/8 待解，记录但不视为本任务回归）。

- [ ] **Step 4: Commit**

```bash
git add AspNetCore.RabbitMq/RabbitMqConsumerBase.cs AspNetCore.Test3/DemoConsumer.cs
git commit -m "refactor(rabbitmq): consumer uses pool lease, auto-declares topology + DLX, IDisposable"
```

---

## Task 6: Outbox 实体加字段

**Files:**
- Modify: `AspNetCore.RabbitMq/RabbitMqOutboxMessage.cs`

- [ ] **Step 1: 在 LastError 属性后追加**

在 `RabbitMqOutboxMessage.cs` 第 83 行 `public string? LastError { get; set; }` 之后、第 84 行 `}` 之前插入：

```csharp

        /// <summary>
        /// 下次允许重试时间，null 表示立即可重试
        /// </summary>
        public DateTimeOffset? NextAttemptAt { get; set; }

        /// <summary>
        /// 是否已转死信
        /// </summary>
        public bool DeadLettered { get; set; }
```

- [ ] **Step 2: 构建检查（库，仍含 Dispatcher/Store 旧签名错误）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 仅 Dispatcher/Store 签名不匹配错误（Task 7/8 解），无实体相关新错误。

- [ ] **Step 3: Commit**

```bash
git add AspNetCore.RabbitMq/RabbitMqOutboxMessage.cs
git commit -m "feat(rabbitmq): add NextAttemptAt and DeadLettered to outbox message"
```

---

## Task 7: Outbox Store 接口扩展 + 内存实现

**Files:**
- Modify: `AspNetCore.RabbitMq/IRabbitMqOutboxStore.cs`
- Modify: `AspNetCore.RabbitMq/InMemoryRabbitMqOutboxStore.cs`

- [ ] **Step 1: 修改 IRabbitMqOutboxStore 接口**

将 `GetPendingAsync` 与 `MarkAsFailedAsync` 改签名，并新增 `MarkAsDeadLetterAsync`：

```csharp
        /// <summary>
        /// 获取待处理的消息
        /// </summary>
        /// <param name="now">当前时间，用于过滤未到重试时间的消息</param>
        /// <param name="takeCount">要获取的消息数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>待处理消息的只读列表</returns>
        /// <remarks>
        /// 返回未发布、未转死信、且重试时间已到（或无重试时间）的消息，按创建时间排序。
        /// </remarks>
        Task<IReadOnlyList<RabbitMqOutboxMessage>> GetPendingAsync(DateTimeOffset now, int takeCount, CancellationToken cancellationToken = default);
```

```csharp
        /// <summary>
        /// 标记消息为发布失败
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="error">错误信息</param>
        /// <param name="nextAttemptAt">下次允许重试时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task MarkAsFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default);
```

新增方法（放在 `MarkAsFailedAsync` 之后）：

```csharp
        /// <summary>
        /// 标记消息为已转死信
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task MarkAsDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: 修改 InMemoryRabbitMqOutboxStore 实现**

将 `GetPendingAsync` 与 `MarkAsFailedAsync` 改实现，并新增 `MarkAsDeadLetterAsync`：

```csharp
        /// <summary>
        /// 获取待处理的消息
        /// </summary>
        /// <remarks>
        /// 1. 筛选未发布且未转死信的消息
        /// 2. 过滤重试时间未到的消息（NextAttemptAt > now）
        /// 3. 按创建时间排序，限制返回数量
        /// </remarks>
        public Task<IReadOnlyList<RabbitMqOutboxMessage>> GetPendingAsync(DateTimeOffset now, int takeCount, CancellationToken cancellationToken = default)
        {
            var result = _messages.Values
                .Where(x => x.PublishedAt is null && !x.DeadLettered
                    && (x.NextAttemptAt is null || x.NextAttemptAt <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(takeCount)
                .ToArray();

            return Task.FromResult<IReadOnlyList<RabbitMqOutboxMessage>>(result);
        }
```

```csharp
        /// <summary>
        /// 标记消息为发布失败
        /// </summary>
        /// <remarks>
        /// 记录错误信息、增加重试计数、设置下次重试时间
        /// </remarks>
        public Task MarkAsFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.LastError = error;
                msg.RetryCount += 1;
                msg.NextAttemptAt = nextAttemptAt;
            }

            return Task.CompletedTask;
        }
```

新增方法（放在 `MarkAsFailedAsync` 之后）：

```csharp
        /// <summary>
        /// 标记消息为已转死信
        /// </summary>
        /// <remarks>
        /// 设置 DeadLettered 标志，使其不再被 GetPendingAsync 取出
        /// </remarks>
        public Task MarkAsDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.DeadLettered = true;
            }

            return Task.CompletedTask;
        }
```

- [ ] **Step 3: 构建检查（库，Dispatcher 仍用旧签名会报错）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 仅 `RabbitMqOutboxDispatcher` 对 `GetPendingAsync`/`MarkAsFailedAsync` 旧签名的调用错误（Task 8 解）。Store 与实体本身无错误。

- [ ] **Step 4: Commit**

```bash
git add AspNetCore.RabbitMq/IRabbitMqOutboxStore.cs AspNetCore.RabbitMq/InMemoryRabbitMqOutboxStore.cs
git commit -m "feat(rabbitmq): outbox store filters by retry time, marks dead-letter"
```

---

## Task 8: Outbox 调度器重试上限 + 退避 + 死信兜底（库全绿）

**Files:**
- Modify: `AspNetCore.RabbitMq/RabbitMqOutboxDispatcher.cs`

- [ ] **Step 1: 重写 ExecuteAsync 与新增 DeadLetterAsync**

将 `ExecuteAsync` 方法替换为：

```csharp
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 循环处理，直到收到停止信号
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 当前时间，用于过滤未到重试时间的消息
                    var now = DateTimeOffset.UtcNow;

                    // 从存储中获取待处理消息，限制批量大小
                    var messages = await _store.GetPendingAsync(now, _options.OutboxBatchSize, stoppingToken);

                    // 遍历处理每条消息
                    foreach (var message in messages)
                    {
                        // 已达重试上限，直接转死信
                        if (message.RetryCount >= _options.MaxRetryCount)
                        {
                            await DeadLetterAsync(message, "max retry count reached", stoppingToken);
                            continue;
                        }

                        try
                        {
                            // 发布消息到RabbitMQ
                            await _publisher.PublishRawAsync(
                                message.Exchange,
                                message.RoutingKey,
                                message.Body,
                                message.Headers,
                                cancellationToken: stoppingToken);

                            // 标记消息为已发布
                            await _store.MarkAsPublishedAsync(message.Id, DateTimeOffset.UtcNow, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            // 记录发布失败的日志
                            _logger.LogWarning(ex, "Outbox message {OutboxMessageId} publish failed.", message.Id);

                            var newRetry = message.RetryCount + 1;
                            if (newRetry >= _options.MaxRetryCount)
                            {
                                // 达到重试上限，转死信
                                await DeadLetterAsync(message, ex.Message, stoppingToken);
                            }
                            else
                            {
                                // 指数退避：base * 2^newRetry，封顶 RetryMaxDelay
                                var exp = Math.Min(newRetry, 30);
                                var backoffTicks = Math.Min(
                                    _options.RetryMaxDelay.Ticks,
                                    _options.RetryBaseDelay.Ticks * (1L << exp));
                                var nextAttempt = DateTimeOffset.UtcNow + TimeSpan.FromTicks(backoffTicks);

                                // 标记失败，设置下次重试时间
                                await _store.MarkAsFailedAsync(message.Id, ex.Message, nextAttempt, stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录获取消息时的异常
                    _logger.LogError(ex, "Error processing outbox messages.");
                }

                try
                {
                    // 等待指定的调度间隔
                    await Task.Delay(_options.OutboxDispatchInterval, stoppingToken);
                }
                catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 当收到停止信号时，退出循环
                    break;
                }
            }
        }
```

新增私有方法 `DeadLetterAsync`（放在 `ExecuteAsync` 之后）：

```csharp
        /// <summary>
        /// 将消息转死信：标记 DeadLettered 并尝试发布到死信交换机
        /// </summary>
        /// <param name="message">原消息</param>
        /// <param name="reason">死信原因</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task DeadLetterAsync(RabbitMqOutboxMessage message, string reason, CancellationToken cancellationToken)
        {
            await _store.MarkAsDeadLetterAsync(message.Id, cancellationToken);
            _logger.LogWarning("Outbox message {OutboxMessageId} dead-lettered: {Reason}", message.Id, reason);

            try
            {
                // 将原消息体发布到死信交换机（保留原 Headers）
                await _publisher.PublishRawAsync(
                    _options.DeadLetterExchange,
                    _options.DeadLetterRoutingKey,
                    message.Body,
                    message.Headers,
                    cancellationToken: cancellationToken);
            }
            catch (Exception dlx)
            {
                // 死信发布失败仅记日志，消息保持 DeadLettered 不再重试
                _logger.LogWarning(dlx, "Dead-letter publish failed for outbox message {OutboxMessageId}.", message.Id);
            }
        }
```

- [ ] **Step 2: 构建检查（库全绿）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: **0 个错误**（生成成功）。库完整编译通过。

- [ ] **Step 3: Commit**

```bash
git add AspNetCore.RabbitMq/RabbitMqOutboxDispatcher.cs
git commit -m "feat(rabbitmq): outbox dispatcher retry cap with exponential backoff and dead-letter routing"
```

---

## Task 9: DI 注册 keyed 双通道池

**Files:**
- Modify: `AspNetCore.RabbitMq/ServiceCollectionExtensions.cs`

- [ ] **Step 1: 重写 AddUnifiedRabbitMq**

全文替换为：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspNetCore.RabbitMq
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddUnifiedRabbitMq(
            this IServiceCollection services,
            Action<RabbitMqOptions> configure)
        {
            var options = new RabbitMqOptions();
            configure(options);

            services.AddSingleton(options);
            services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();

            // 发布者通道池
            services.AddKeyedSingleton<IRabbitMqChannelPool>("publisher", (sp, _) =>
                new RabbitMqChannelPool(
                    sp.GetRequiredService<IRabbitMqConnection>(),
                    options.ChannelPoolSize));

            // 消费者通道池
            services.AddKeyedSingleton<IRabbitMqChannelPool>("consumer", (sp, _) =>
                new RabbitMqChannelPool(
                    sp.GetRequiredService<IRabbitMqConnection>(),
                    options.ConsumerChannelPoolSize));

            // 发布者注入发布者池与配置
            services.AddSingleton<IRabbitMqPublisher>(sp =>
                new RabbitMqPublisher(
                    sp.GetRequiredKeyedService<IRabbitMqChannelPool>("publisher"),
                    options));

            services.TryAddSingleton<IRabbitMqOutboxStore, InMemoryRabbitMqOutboxStore>();
            services.AddSingleton<IRabbitMqOutbox, RabbitMqOutbox>();
            services.AddHostedService<RabbitMqOutboxDispatcher>();

            return services;
        }
    }
}
```

- [ ] **Step 2: 构建检查（库全绿）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 0 个错误。

- [ ] **Step 3: Commit**

```bash
git add AspNetCore.RabbitMq/ServiceCollectionExtensions.cs
git commit -m "refactor(rabbitmq): register keyed publisher/consumer channel pools, wire publisher"
```

---

## Task 10: 删除 Test3 孤儿托管服务

**Files:**
- Delete: `AspNetCore.Test3/RabbitMqHostedService.cs`

- [ ] **Step 1: 删除文件**

Run: `rm AspNetCore.Test3/RabbitMqHostedService.cs`

- [ ] **Step 2: 构建检查（库仍全绿；Test3 因 DemoMessage 缺失仍红，属范围外）**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 0 个错误。

- [ ] **Step 3: Commit**

```bash
git add -A AspNetCore.Test3/RabbitMqHostedService.cs
git commit -m "chore(test3): remove orphan RabbitMqHostedService"
```

---

## Task 11: 最终验证

**Files:** 无（验证）

- [ ] **Step 1: 库完整构建**

Run: `dotnet build AspNetCore.RabbitMq/AspNetCore.RabbitMq.csproj`
Expected: 0 个错误，0 个警告（或仅既存警告）。

- [ ] **Step 2: 对齐项目文档（spec 第 5 节策略 B：实现完成后一次性对齐）**

更新 `docs/02-RabbitMq-消息队列库.md`：删除所有"TODO（编译）"编译阻断标记；更新配置表（§3）补全新增 10 属性；更新发布者/消费者/Outbox 章节为重构后行为；更新待确认事项为已实现说明。详见 memory `rabbitmq-doc-alignment-deferred`。

- [ ] **Step 3: （可选，需 live RabbitMQ broker + 先修 Test3 DemoMessage 范围外问题）Test3 E2E**

Run: `dotnet run --project AspNetCore.Test3`（需本地 RabbitMQ localhost:5672 + rabbitmq_delayed_message_exchange 插件）
Expected: 控制台输出 "已发送..." 且 DemoConsumer 收到两条消息。

> 此步骤依赖范围外的 Test3 类型对齐（spec 第 6 节），非本计划交付项。库编译通过即为本计划完成标准。

---

## Self-Review 自检结果

1. **Spec 覆盖**：
   - confirm 实现 → Task 2/3/4 ✓
   - delayMs 实现 → Task 4 ✓
   - 死信 DLX（消费者侧）→ Task 5 ✓
   - Outbox 重试上限+退避+死信兜底 → Task 6/7/8 ✓
   - 双通道池隔离 → Task 3/9 ✓
   - 字符串 UTF-8 序列化 → Task 4 ✓
   - 消费者自动声明拓扑 → Task 5 ✓
   - 交换机 type 虚属性 → Task 5 ✓
   - 删 Test3 孤儿 → Task 10 ✓
   - 编译阻断修复 → Task 4 ✓

2. **Placeholder 扫描**：Task 3 Step 2 经自检发现"重建 tracker 重复订阅"bug，已修正为 `ReturnAsync` 收 tracker（接口改 `ReturnAsync(channel, tracker)`）。无遗留 TODO。

3. **类型一致性**：`MarkAsFailedAsync(messageId, error, nextAttemptAt, ct)` 在 Task 7 定义、Task 8 调用一致 ✓；`GetPendingAsync(now, takeCount, ct)` 一致 ✓；`MarkAsDeadLetterAsync(messageId, ct)` 一致 ✓；`PooledChannelLease.Tracker` 为 `internal ChannelConfirmTracker?`，`DisposeAsync` 用 `Tracker!` ✓。

4. **顺序问题**：Task 4/5/6/7 在中间检查点会因后续任务未完成而报错（Store/Dispatcher 签名迁移），已在各 Step 标注"预期仅 X 相关错误"。**真正全绿检查点为 Task 8 Step 2**。建议执行时 Task 4-7 连续执行后统一在 Task 8 验证全绿。
