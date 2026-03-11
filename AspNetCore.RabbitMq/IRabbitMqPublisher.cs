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
        /// <param name="message">消息内容</param>
        /// <param name="props">消息属性配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步发布操作的任务</returns>
        /// <remarks>
        /// 此方法将消息序列化后发布到指定的交换机和路由键。
        /// 如果消息是字符串类型，直接使用UTF-8编码；否则使用JSON序列化。
        /// 可以通过props参数自定义消息属性，如持久化、过期时间等。
        /// </remarks>
        /// <exception cref="ArgumentNullException">当exchange或routingKey为null时抛出</exception>
        /// <exception cref="RabbitMQ.Client.Exceptions.BrokerUnreachableException">当无法连接到RabbitMQ服务器时抛出</exception>
        /// <exception cref="RabbitMQ.Client.Exceptions.AlreadyClosedException">当RabbitMQ连接已关闭时抛出</exception>
        ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default);

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
        /// 此方法直接发布原始字节数据到指定的交换机和路由键，不进行序列化处理。
        /// 适用于已经序列化好的消息，或者需要自定义序列化格式的场景。
        /// 可以通过headers参数设置消息头，通过props参数自定义消息属性。
        /// </remarks>
        /// <exception cref="ArgumentNullException">当exchange或routingKey为null时抛出</exception>
        /// <exception cref="RabbitMQ.Client.Exceptions.BrokerUnreachableException">当无法连接到RabbitMQ服务器时抛出</exception>
        /// <exception cref="RabbitMQ.Client.Exceptions.AlreadyClosedException">当RabbitMQ连接已关闭时抛出</exception>
        ValueTask PublishRawAsync(
            string exchange,
            string routingKey,
            ReadOnlyMemory<byte> body,
            IDictionary<string, object?>? headers = null,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default);
    }
}
