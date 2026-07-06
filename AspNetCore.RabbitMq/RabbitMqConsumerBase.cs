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
    /// </remarks>
    public abstract class RabbitMqConsumerBase<T> : IRabbitMqConsumer, IAsyncDisposable where T : class
    {
        private readonly IRabbitMqChannelPool _channelPool;
        private readonly RabbitMqOptions _options;
        private PooledChannelLease? _lease;
        private IChannel? _channel;
        private string? _consumerTag;

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
            if (_lease is not null)
            {
                throw new InvalidOperationException("Consumer already started; dispose before restarting.");
            }

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
                if (_options.DefaultMessageTTL is { } ttl)
                {
                    args["x-message-ttl"] = (int)ttl.TotalMilliseconds;
                }

                // 声明死信交换机、队列并绑定
                await _channel.ExchangeDeclareAsync(_options.DeadLetterExchange, RabbitMQ.Client.ExchangeType.Direct, durable: true, autoDelete: false);
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
                // 从消息头恢复 traceparent 链路：HandleAsync 内 Activity.Current.TraceId 与发布端一致，
                // ILogger（经 ActivityTraceIdEnricher）按 TraceId 串联。using 确保回调结束恢复原 Activity
                using var activity = RabbitMqTracing.ExtractAndStartActivity(ea.BasicProperties.Headers);
                try
                {
                    var msg = JsonSerializer.Deserialize<T>(ea.Body.Span)!;
                    await HandleAsync(msg, cancellationToken);
                    await ch.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch
                {
                    await ch.BasicNackAsync(ea.DeliveryTag, false, true);
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
        /// 先 BasicCancel 停止消费，使通道不再有在途投递，归还后可被其他消费者复用。
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

            await lease.DisposeAsync();
        }
    }
}
