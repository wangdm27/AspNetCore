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
        /// <param name="takeCount">要获取的消息数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>待处理消息的只读列表</returns>
        /// <remarks>
        /// 通常返回未发布的消息，按创建时间排序
        /// 用于后台处理器批量获取消息进行发布
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">当takeCount小于等于0时抛出</exception>
        Task<IReadOnlyList<RabbitMqOutboxMessage>> GetPendingAsync(int takeCount, CancellationToken cancellationToken = default);
        
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
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <remarks>
        /// 当消息发布失败时调用此方法
        /// 记录错误信息，通常还会增加重试计数
        /// 用于后续的重试逻辑
        /// </remarks>
        /// <exception cref="ArgumentException">当messageId为空时抛出</exception>
        /// <exception cref="ArgumentNullException">当error为null时抛出</exception>
        Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
    }
}