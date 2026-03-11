namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ消费者接口
    /// </summary>
    /// <remarks>
    /// 定义了RabbitMQ消费者的基本操作，主要用于启动消费者监听消息
    /// </remarks>
    public interface IRabbitMqConsumer
    {
        /// <summary>
        /// 启动消费者
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步启动操作的任务</returns>
        /// <remarks>
        /// 此方法启动消费者，开始监听指定队列的消息
        /// 当取消令牌被触发时，消费者会停止监听
        /// </remarks>
        /// <exception cref="RabbitMQ.Client.Exceptions.BrokerUnreachableException">当无法连接到RabbitMQ服务器时抛出</exception>
        /// <exception cref="RabbitMQ.Client.Exceptions.AlreadyClosedException">当RabbitMQ连接已关闭时抛出</exception>
        Task StartAsync(CancellationToken cancellationToken = default);
    }
}
