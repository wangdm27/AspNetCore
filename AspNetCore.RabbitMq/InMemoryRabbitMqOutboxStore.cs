using System.Collections.Concurrent;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// 内存中的RabbitMQ发件箱存储实现
    /// </summary>
    /// <remarks>
    /// 实现了IRabbitMqOutboxStore接口，使用内存存储来实现发件箱模式
    /// 适用于开发和测试环境，或对持久性要求不高的场景
    /// </remarks>
    internal sealed class InMemoryRabbitMqOutboxStore : IRabbitMqOutboxStore
    {
        /// <summary>
        /// 存储消息的并发字典
        /// </summary>
        /// <remarks>
        /// 使用ConcurrentDictionary确保线程安全，键为消息ID，值为消息对象
        /// </remarks>
        private readonly ConcurrentDictionary<Guid, RabbitMqOutboxMessage> _messages = new();

        /// <summary>
        /// 添加消息到发件箱
        /// </summary>
        /// <param name="message">要添加的消息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <remarks>
        /// 将消息存储在内存中的并发字典中，使用消息ID作为键
        /// </remarks>
        public Task AddAsync(RabbitMqOutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages[message.Id] = message;
            return Task.CompletedTask;
        }

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

        /// <summary>
        /// 标记消息为已发布
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="publishedAt">发布时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <remarks>
        /// 根据消息ID查找消息并设置PublishedAt时间戳
        /// 静默处理消息不存在的情况
        /// </remarks>
        public Task MarkAsPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.PublishedAt = publishedAt;
            }

            return Task.CompletedTask;
        }

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
    }
}