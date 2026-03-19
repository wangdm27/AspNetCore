using System.Text.Json;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ发件箱实现
    /// </summary>
    /// <remarks>
    /// 实现了IRabbitMqOutbox接口，负责将消息加入发件箱存储
    /// 支持对象序列化和消息属性配置
    /// </remarks>
    internal sealed class RabbitMqOutbox : IRabbitMqOutbox
    {
        /// <summary>
        /// 发件箱存储实例
        /// </summary>
        private readonly IRabbitMqOutboxStore _store;

        /// <summary>
        /// 初始化发件箱
        /// </summary>
        /// <param name="store">发件箱存储实例</param>
        public RabbitMqOutbox(IRabbitMqOutboxStore store)
        {
            _store = store;
        }

        /// <summary>
        /// 将消息加入发件箱
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="exchange">交换机名称</param>
        /// <param name="routingKey">路由键</param>
        /// <param name="message">消息内容</param>
        /// <param name="props">消息属性配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步入队操作的任务</returns>
        /// <remarks>
        /// 此方法执行以下操作：
        /// 1. 创建消息属性对象
        /// 2. 应用自定义属性配置
        /// 3. 序列化消息内容为JSON字节
        /// 4. 创建发件箱消息对象
        /// 5. 将消息添加到存储中
        /// </remarks>
        /// <exception cref="ArgumentNullException">当exchange或routingKey为null时抛出</exception>
        /// <exception cref="ArgumentException">当exchange或routingKey为空字符串时抛出</exception>
        public async ValueTask EnqueueAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default)
        {
            // 创建消息属性对象
            var properties = new BasicProperties();
            // 应用自定义属性配置
            props?.Invoke(properties);

            // 创建发件箱消息对象
            var outboxMessage = new RabbitMqOutboxMessage
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                // 序列化消息为JSON字节
                Body = JsonSerializer.SerializeToUtf8Bytes(message),
                // 处理消息头
                Headers = properties.Headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
                    ?? new Dictionary<string, object?>()
            };

            // 将消息添加到存储
            await _store.AddAsync(outboxMessage, cancellationToken);
        }
    }
}