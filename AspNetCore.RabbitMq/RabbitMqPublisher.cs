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
            TaskCompletionSource<PublishConfirmResult>? tcs = null;
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
                    var result = await tracker!.WaitAsync(seq, _options.PublisherConfirmTimeout, cancellationToken);
                    switch (result)
                    {
                        case PublishConfirmResult.Nacked:
                            throw new RabbitMqPublishNackedException(seq);
                        case PublishConfirmResult.TimedOut:
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
