using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// 将消息加入RabbitMQ发件箱
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="exchange">交换机名称</param>
    /// <param name="routingKey">路由键</param>
    /// <param name="message">消息内容</param>
    /// <param name="props">消息属性配置委托</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步入队操作的任务</returns>
    /// <remarks>
    /// 此方法将消息添加到发件箱存储中，由后台处理器负责发布到RabbitMQ。
    /// 发件箱模式确保消息不会因为系统故障或网络问题而丢失，
    /// 即使在消息发布失败的情况下，也会进行重试。
    /// 消息默认会被序列化为JSON格式。
    /// </remarks>
    /// <exception cref="ArgumentNullException">当exchange或routingKey为null时抛出</exception>
    /// <exception cref="ArgumentException">当exchange或routingKey为空字符串时抛出</exception>
    public interface IRabbitMqOutbox
    {
        ValueTask EnqueueAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default);
    }
}
