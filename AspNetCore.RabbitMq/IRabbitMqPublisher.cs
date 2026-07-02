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
