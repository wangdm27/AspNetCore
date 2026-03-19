using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                    // 从存储中获取待处理消息，限制批量大小
                    var messages = await _store.GetPendingAsync(_options.OutboxBatchSize, stoppingToken);
                    
                    // 遍历处理每条消息
                    foreach (var message in messages)
                    {
                        try
                        {
                            // 发布消息到RabbitMQ
                            await _publisher.PublishRawAsync(
                                message.Exchange,
                                message.RoutingKey,
                                message.Body,
                                message.Headers,
                                cancellationToken: stoppingToken);

                            // 标记消息为已发布
                            await _store.MarkAsPublishedAsync(message.Id, DateTimeOffset.UtcNow, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            // 记录发布失败的日志
                            _logger.LogWarning(ex, "Outbox message {OutboxMessageId} publish failed.", message.Id);
                            // 标记消息为发布失败
                            await _store.MarkAsFailedAsync(message.Id, ex.Message, stoppingToken);
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
    }
}