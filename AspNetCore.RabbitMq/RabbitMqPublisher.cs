using System.Text.Json;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ消息发布者实现
    /// </summary>
    /// <remarks>
    /// 负责将消息发布到RabbitMQ交换机，支持对象序列化和原始字节发布
    /// </remarks>
    internal sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        /// <summary>
        /// 通道池实例
        /// </summary>
        private readonly IRabbitMqChannelPool _channelPool;

        /// <summary>
        /// 初始化消息发布者
        /// </summary>
        /// <param name="channelPool">通道池实例</param>
        public RabbitMqPublisher(IRabbitMqChannelPool channelPool)
        {
            _channelPool = channelPool;
        }

        /// <summary>
        /// 发布消息到RabbitMQ
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="exchange">交换机名称</param>
        /// <param name="routingKey">路由键</param>
        /// <param name="message">消息内容</param>
        /// <param name="props">消息属性配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步发布操作的任务</returns>
        /// <remarks>
        /// 此方法将消息序列化为JSON字节，然后调用PublishRawAsync发布
        /// </remarks>
        public ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            return PublishRawAsync(exchange, routingKey, body, null, props, cancellationToken);
        }

        /// <summary>
        /// 发布原始字节消息到RabbitMQ
        /// </summary>
        /// <param name="exchange">交换机名称</param>
        /// <param name="routingKey">路由键</param>
        /// <param name="body">原始消息体字节数据</param>
        /// <param name="headers">消息头字典</param>
        /// <param name="props">消息属性配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步发布操作的任务</returns>
        /// <remarks>
        /// 此方法从通道池获取通道，设置消息属性，然后发布消息
        /// 消息默认设置为持久化
        /// </remarks>
        public async ValueTask PublishRawAsync(
            string exchange,
            string routingKey,
            ReadOnlyMemory<byte> body,
            IDictionary<string, object?>? headers = null,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default)
        {
            // 从通道池获取通道
            await using var lease = await _channelPool.RentAsync(cancellationToken);

            // 创建消息属性，默认设置为持久化
            var properties = new BasicProperties
            {
                Persistent = true,
                Headers = headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
            };

            // 应用自定义属性配置
            props?.Invoke(properties);

            // 发布消息到RabbitMQ
            await lease.Channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}