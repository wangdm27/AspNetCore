namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ发件箱存储接口
    /// </summary>
    /// <remarks>
    /// 定义了发件箱消息的存储、检索和状态管理操作
    /// 实现类负责持久化消息并提供可靠的状态管理
    /// </remarks>
    public interface IRabbitMqOutboxStore
    {
        /// <summary>
        /// 添加消息到发件箱存储
        /// </summary>
        /// <param name="message">要添加的消息对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步添加操作的任务</returns>
        /// <remarks>
        /// 将消息持久化到存储中，等待后续处理
        /// </remarks>
        /// <exception cref="ArgumentNullException">当message为null时抛出</exception>
        Task AddAsync(RabbitMqOutboxMessage message, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 获取待处理的消息
        /// </summary>
        /// <param name="now">当前时间，用于过滤未到重试时间的消息</param>
        /// <param name="takeCount">要获取的消息数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>待处理消息的只读列表</returns>
        /// <remarks>
        /// 返回未发布、未转死信、且重试时间已到（或无重试时间）的消息，按创建时间排序。
        /// </remarks>
        Task<IReadOnlyList<RabbitMqOutboxMessage>> GetPendingAsync(DateTimeOffset now, int takeCount, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 标记消息为已发布
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="publishedAt">发布时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <remarks>
        /// 当消息成功发布到RabbitMQ后调用此方法
        /// 更新消息状态为已发布，并记录发布时间
        /// </remarks>
        /// <exception cref="ArgumentException">当messageId为空时抛出</exception>
        Task MarkAsPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 标记消息为发布失败
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="error">错误信息</param>
        /// <param name="nextAttemptAt">下次允许重试时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task MarkAsFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记消息为已转死信
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task MarkAsDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default);
    }
}