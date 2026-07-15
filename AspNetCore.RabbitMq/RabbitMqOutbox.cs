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
        /// 在请求上下文捕获 W3C traceparent 与关键 BasicProperties（ContentType/CorrelationId/MessageId）持久化进发件箱：
        /// dispatcher 后台发布时已脱离原请求上下文（<c>Activity.Current</c> 为 null），必须在此处保存，
        /// 消费端才能延续 TraceId，Seq 方可按 TraceId 串联 Api->MQ->消费 全链路。
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
            ArgumentException.ThrowIfNullOrEmpty(exchange);
            ArgumentException.ThrowIfNullOrEmpty(routingKey);

            var properties = new BasicProperties();
            props?.Invoke(properties);

            // 捕获请求上下文 traceparent：dispatcher 发布时 Activity.Current 已非原请求，必须在此持久化
            var headers = properties.Headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
                          ?? new Dictionary<string, object?>();
            RabbitMqTracing.Inject(headers);

            var outboxMessage = new RabbitMqOutboxMessage
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                Body = JsonSerializer.SerializeToUtf8Bytes(message),
                Headers = headers,
                ContentType = properties.ContentType,
                CorrelationId = properties.CorrelationId,
                MessageId = properties.MessageId
            };

            await _store.AddAsync(outboxMessage, cancellationToken);
        }
    }
}