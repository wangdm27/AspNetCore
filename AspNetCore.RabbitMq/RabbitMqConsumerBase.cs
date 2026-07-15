using System.Collections.Concurrent;
using System.Security.Cryptography;
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
    /// 消费者从消费者通道池租用通道，长租持至 Dispose 归还（归还前先 BasicCancel 停止消费，使通道可被复用）。
    /// 子类需实现队列、交换机、路由键配置，以及消息处理逻辑。
    /// 消费异常按 <see cref="RabbitMqOptions.ConsumerMaxRetryCount"/> 限量重试（requeue），超限 nack(requeue:false) 交死信队列或丢弃，避免毒消息死循环。
    /// </remarks>
    public abstract class RabbitMqConsumerBase<T> : IRabbitMqConsumer, IAsyncDisposable where T : class
    {
        private readonly IRabbitMqChannelPool _channelPool;
        private readonly RabbitMqOptions _options;
        private PooledChannelLease? _lease;
        private IChannel? _channel;
        private string? _consumerTag;
        private CancellationTokenSource? _cts;

        /// <summary>毒消息重试计数，key = MessageId（空则 body SHA256）。进程内 best-effort，重启重置。</summary>
        private readonly ConcurrentDictionary<string, int> _retryCounts = new();

        /// <summary>在途回调计数，Dispose 时据此排空，避免在已归还通道上 Ack/Nack。</summary>
        private int _inflight;

        /// <summary>Dispose 排空信号；仅 Dispose 期间非 null。</summary>
        private TaskCompletionSource? _drainTcs;

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
        /// 4. 可选设置 x-message-ttl（与死信解耦，可单独使用）
        /// 5. 声明主队列
        /// 6. 绑定主队列到交换机
        /// 7. 设置QoS
        /// 8. 创建消费者并注册消息接收事件
        /// 9. 开始消费消息
        /// </remarks>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_lease is not null)
            {
                throw new InvalidOperationException("Consumer already started; dispose before restarting.");
            }

            // per-consumer 取消令牌：Dispose 时取消以中断在途 HandleAsync（OCE 分支保消息回队列），
            // 与 StartAsync 入参 token 解耦，避免取消信号波及所有在途消息的处理语义。
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _lease = await _channelPool.RentAsync(cancellationToken);
            _channel = _lease.Value.Channel;
            var ch = _channel;

            // 声明交换机
            await _channel.ExchangeDeclareAsync(Exchange, ExchangeType, durable: true, autoDelete: false);

            // 主队列参数（含可选死信参数）
            var args = new Dictionary<string, object?>();
            if (_options.EnableDeadLetter)
            {
                args["x-dead-letter-exchange"] = _options.DeadLetterExchange;
                args["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey;

                // 声明死信交换机、队列并绑定
                await _channel.ExchangeDeclareAsync(_options.DeadLetterExchange, RabbitMQ.Client.ExchangeType.Direct, durable: true, autoDelete: false);
                await _channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
                await _channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterRoutingKey);
            }

            if (_options.DefaultMessageTTL is { } ttl)
            {
                // x-message-ttl 为 32 位毫秒，封顶 int.MaxValue（约 24.85 天）；与 EnableDeadLetter 解耦，可单独使用
                args["x-message-ttl"] = (int)Math.Min(ttl.TotalMilliseconds, int.MaxValue);
            }

            // 声明主队列（带可选死信/TTL 参数）
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
                // 从消息头恢复 traceparent 链路：HandleAsync 内 Activity.Current.TraceId 与发布端一致，
                // ILogger（经 ActivityTraceIdEnricher）按 TraceId 串联。using 确保回调结束恢复原 Activity
                using var activity = RabbitMqTracing.ExtractAndStartActivity(ea.BasicProperties.Headers);
                Interlocked.Increment(ref _inflight);
                try
                {
                    var msg = JsonSerializer.Deserialize<T>(ea.Body.Span)!;
                    await HandleAsync(msg, _cts!.Token);
                    await ch.BasicAckAsync(ea.DeliveryTag, false);
                    _retryCounts.TryRemove(GetRetryKey(ea), out int _);
                }
                catch (OperationCanceledException)
                {
                    // 关停/取消：保消息回队列，不计重试。关停期（_lease 已被 Dispose 置空）跳过 nack，
                    // 避免残余回调在通道已归还/复用后仍触达，造成 deliveryTag 语义错乱。
                    if (_lease is not null)
                    {
                        try { await ch.BasicNackAsync(ea.DeliveryTag, false, requeue: true); } catch { }
                    }
                }
                catch (Exception)
                {
                    var key = GetRetryKey(ea);
                    var count = _retryCounts.AddOrUpdate(key, 1, (_, c) => c + 1);
                    if (count >= _options.ConsumerMaxRetryCount)
                    {
                        // 超上限：requeue:false 交 DLX 接管或丢弃，避免毒消息死循环
                        try { await ch.BasicNackAsync(ea.DeliveryTag, false, requeue: false); } catch { }
                        _retryCounts.TryRemove(key, out int _);
                    }
                    else
                    {
                        try { await ch.BasicNackAsync(ea.DeliveryTag, false, requeue: true); } catch { }
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref _inflight) == 0)
                    {
                        _drainTcs?.TrySetResult();
                    }
                }
            };

            // 开始消费消息，broker 返回实际消费者标签
            _consumerTag = await _channel.BasicConsumeAsync(
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
        /// <remarks>
        /// 先取消在途 HandleAsync，再 BasicCancel 停止消费，排空在途回调，最后归还租约。
        /// 排空避免回调在通道已归还/复用后仍调 Ack/Nack。
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (_lease is not { } lease)
            {
                return;
            }

            var channel = _channel;
            var tag = _consumerTag;
            _lease = null;
            _channel = null;
            _consumerTag = null;

            // 排空在途回调。先建 _drainTcs 再 Cancel，确保回调 finally 的 TrySetResult 不会因 TCS 尚未赋值而 miss。
            _drainTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // 取消在途 HandleAsync（OCE 分支保消息回队列；关停期 _lease 已置空会跳过 nack）
            _cts?.Cancel();

            if (channel is not null && channel.IsOpen && tag is not null)
            {
                try
                {
                    await channel.BasicCancelAsync(tag, noWait: false, cancellationToken: default);
                }
                catch
                {
                    // 通道已关闭等情况忽略，归还时由池清理
                }
            }

            // 排空等待（带超时兜底，避免异常情况卡住关停）。double-check inflight：建 TCS 与读之间若已归零直接放行。
            if (Volatile.Read(ref _inflight) > 0)
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                timeoutCts.Token.Register(() => _drainTcs.TrySetCanceled());
                try
                {
                    await _drainTcs.Task;
                }
                catch (OperationCanceledException)
                {
                    // 排空超时：继续归还。残余回调的 Ack 在 try 内已 await 完或抛；
                    // OCE 残余 nack 因 _lease 置空被跳过，不会触达已复用通道。
                }
            }

            await lease.DisposeAsync();

            _cts?.Dispose();
            _cts = null;
            _drainTcs = null;
        }

        /// <summary>
        /// 重试计数 key：优先 MessageId，空则 body SHA256（无 MessageId 时的稳定标识）。
        /// </summary>
        private static string GetRetryKey(BasicDeliverEventArgs ea)
        {
            var id = ea.BasicProperties.MessageId;
            return !string.IsNullOrEmpty(id)
                ? id
                : Convert.ToHexString(SHA256.HashData(ea.Body.Span));
        }
    }
}
