using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ 信道池抽象。
    /// 通过租借（Rent）方式提供可复用的 <see cref="IChannel"/>，
    /// 调用方在使用完成后释放租约以归还信道。
    /// </summary>
    public interface IRabbitMqChannelPool : IAsyncDisposable
    {
        /// <summary>
        /// 从池中异步租借一个信道。
        /// 返回值是一个租约对象，租约释放时会自动把信道归还到池中。
        /// </summary>
        /// <param name="cancellationToken">用于取消等待租借操作。</param>
        /// <returns>包含信道和归还逻辑的租约。</returns>
        ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 信道租约：封装一个可用的 <see cref="IChannel"/> 与归还动作。
    /// 建议使用 await using 来确保最终归还。
    /// </summary>
    public readonly struct PooledChannelLease : IAsyncDisposable
    {
        private readonly IRabbitMqChannelPoolLease _lease;

        internal PooledChannelLease(IChannel channel, ChannelConfirmTracker tracker, IRabbitMqChannelPoolLease lease)
        {
            Channel = channel;
            Tracker = tracker;
            _lease = lease;
        }

        /// <summary>
        /// 当前租约持有的 RabbitMQ 信道。
        /// </summary>
        public IChannel Channel { get; }

        /// <summary>
        /// 该通道的发布确认追踪器（消费者可不使用）。
        /// </summary>
        internal ChannelConfirmTracker? Tracker { get; }

        /// <summary>
        /// 释放租约并将信道归还给池（不会直接关闭信道）。
        /// </summary>
        public ValueTask DisposeAsync() => _lease.ReturnAsync(Channel, Tracker!);
    }

    /// <summary>
    /// 池内部使用的归还通道契约，对外隐藏具体实现。
    /// </summary>
    internal interface IRabbitMqChannelPoolLease
    {
        /// <summary>
        /// 将租借的信道与追踪器归还到池中。
        /// </summary>
        ValueTask ReturnAsync(IChannel channel, ChannelConfirmTracker tracker);
    }
}
