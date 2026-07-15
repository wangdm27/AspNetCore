using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ发件箱后台调度器
    /// </summary>
    /// <remarks>
    /// 继承自BackgroundService，作为后台服务运行
    /// 定期从发件箱存储中获取待处理消息并发布到RabbitMQ
    /// 处理消息发布失败的情况，记录错误并标记为失败
    /// </remarks>
    internal sealed class RabbitMqOutboxDispatcher : BackgroundService
    {
        /// <summary>
        /// 发件箱存储实例
        /// </summary>
        private readonly IRabbitMqOutboxStore _store;
        
        /// <summary>
        /// RabbitMQ消息发布者
        /// </summary>
        private readonly IRabbitMqPublisher _publisher;
        
        /// <summary>
        /// RabbitMQ配置选项
        /// </summary>
        private readonly RabbitMqOptions _options;
        
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<RabbitMqOutboxDispatcher> _logger;

        /// <summary>
        /// 初始化发件箱调度器
        /// </summary>
        /// <param name="store">发件箱存储实例</param>
        /// <param name="publisher">RabbitMQ消息发布者</param>
        /// <param name="options">RabbitMQ配置选项</param>
        /// <param name="logger">日志记录器</param>
        public RabbitMqOutboxDispatcher(
            IRabbitMqOutboxStore store,
            IRabbitMqPublisher publisher,
            RabbitMqOptions options,
            ILogger<RabbitMqOutboxDispatcher> logger)
        {
            _store = store;
            _publisher = publisher;
            _options = options;
            _logger = logger;

            if (store is InMemoryRabbitMqOutboxStore)
            {
                _logger.LogWarning(
                    "InMemory outbox store in use; not durable across restarts. " +
                    "Register a persistent IRabbitMqOutboxStore for production.");
            }
        }

        /// <summary>
        /// 执行后台任务
        /// </summary>
        /// <param name="stoppingToken">停止令牌</param>
        /// <returns>表示后台任务的任务</returns>
        /// <remarks>
        /// 此方法执行以下操作：
        /// 1. 循环检查是否需要停止
        /// 2. 从发件箱存储中获取待处理消息
        /// 3. 遍历消息并尝试发布
        /// 4. 根据发布结果更新消息状态
        /// 5. 等待指定的调度间隔
        /// 6. 处理停止请求
        /// </remarks>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 循环处理，直到收到停止信号
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 当前时间，用于过滤未到重试时间的消息
                    var now = DateTimeOffset.UtcNow;

                    // 从存储中获取待处理消息，限制批量大小
                    var messages = await _store.GetPendingAsync(now, _options.OutboxBatchSize, stoppingToken);

                    // 遍历处理每条消息
                    foreach (var message in messages)
                    {
                        // 已达重试上限，直接转死信
                        if (message.RetryCount >= _options.MaxRetryCount)
                        {
                            await DeadLetterAsync(message, "max retry count reached", stoppingToken);
                            continue;
                        }

                        try
                        {
                            await _publisher.PublishRawAsync(
                                message.Exchange,
                                message.RoutingKey,
                                message.Body,
                                message.Headers,
                                props: BuildPropsCallback(message),
                                cancellationToken: stoppingToken);

                            await _store.MarkAsPublishedAsync(message.Id, DateTimeOffset.UtcNow, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Outbox message {OutboxMessageId} publish failed.", message.Id);

                            var newRetry = message.RetryCount + 1;
                            if (newRetry >= _options.MaxRetryCount)
                            {
                                // 达到重试上限，转死信
                                await DeadLetterAsync(message, ex.Message, stoppingToken);
                            }
                            else
                            {
                                // 指数退避：base * 2^newRetry，封顶 RetryMaxDelay
                                var exp = Math.Min(newRetry, 30);
                                var backoffTicks = Math.Min(
                                    _options.RetryMaxDelay.Ticks,
                                    _options.RetryBaseDelay.Ticks * (1L << exp));
                                var nextAttempt = DateTimeOffset.UtcNow + TimeSpan.FromTicks(backoffTicks);

                                await _store.MarkAsFailedAsync(message.Id, ex.Message, nextAttempt, stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录获取消息时的异常
                    _logger.LogError(ex, "Error processing outbox messages.");
                }

                try
                {
                    // 等待指定的调度间隔
                    await Task.Delay(_options.OutboxDispatchInterval, stoppingToken);
                }
                catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 当收到停止信号时，退出循环
                    break;
                }
            }
        }

        /// <summary>
        /// 将消息转死信：标记 DeadLettered 并尝试发布到死信交换机
        /// </summary>
        /// <param name="message">原消息</param>
        /// <param name="reason">死信原因</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <remarks>
        /// <see cref="RabbitMqOptions.DeadLetterExchange"/> 为空时不发布，消息保持 DeadLettered 不丢失（仅记告警），
        /// 避免向默认空交换机投递被 broker 静默丢弃。
        /// </remarks>
        private async Task DeadLetterAsync(RabbitMqOutboxMessage message, string reason, CancellationToken cancellationToken)
        {
            await _store.MarkAsDeadLetterAsync(message.Id, cancellationToken);
            _logger.LogWarning("Outbox message {OutboxMessageId} dead-lettered: {Reason}", message.Id, reason);

            if (string.IsNullOrEmpty(_options.DeadLetterExchange))
            {
                _logger.LogWarning(
                    "Outbox message {OutboxMessageId} held as DeadLettered: no DeadLetterExchange configured, not forwarded.",
                    message.Id);
                return;
            }

            try
            {
                await _publisher.PublishRawAsync(
                    _options.DeadLetterExchange,
                    _options.DeadLetterRoutingKey,
                    message.Body,
                    message.Headers,
                    props: BuildPropsCallback(message),
                    cancellationToken: cancellationToken);
            }
            catch (Exception dlx)
            {
                // 死信发布失败仅记日志，消息保持 DeadLettered 不再重试
                _logger.LogWarning(dlx, "Dead-letter publish failed for outbox message {OutboxMessageId}.", message.Id);
            }
        }

        /// <summary>
        /// 构造发布属性回调：从 outbox 消息重建非 Header 的 BasicProperties 字段（ContentType/CorrelationId/MessageId）。
        /// </summary>
        private static Action<IBasicProperties> BuildPropsCallback(RabbitMqOutboxMessage message) => properties =>
        {
            if (!string.IsNullOrEmpty(message.ContentType))
            {
                properties.ContentType = message.ContentType;
            }

            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                properties.CorrelationId = message.CorrelationId;
            }

            if (!string.IsNullOrEmpty(message.MessageId))
            {
                properties.MessageId = message.MessageId;
            }
        };
    }
}