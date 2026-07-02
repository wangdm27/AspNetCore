using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ通道池实现
    /// </summary>
    /// <remarks>
    /// 管理RabbitMQ通道的创建、复用和释放，提高性能并减少资源消耗。
    /// 所有通道创建时开启发布确认模式并绑定 ChannelConfirmTracker。
    /// </remarks>
    internal sealed class RabbitMqChannelPool : IRabbitMqChannelPool, IRabbitMqChannelPoolLease
    {
        private readonly IRabbitMqConnection _connection;
        private readonly ConcurrentQueue<(IChannel Channel, ChannelConfirmTracker Tracker)> _pool = new();
        private readonly SemaphoreSlim _gate;
        private volatile bool _disposed;

        /// <summary>
        /// 初始化通道池
        /// </summary>
        /// <param name="connection">RabbitMQ连接实例</param>
        /// <param name="poolSize">通道池大小（由调用方传入，区分发布者/消费者池）</param>
        public RabbitMqChannelPool(IRabbitMqConnection connection, int poolSize)
        {
            _connection = connection;
            _gate = new SemaphoreSlim(poolSize, poolSize);
        }

        /// <summary>
        /// 从通道池获取一个通道
        /// </summary>
        public async ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _gate.WaitAsync(cancellationToken);

            try
            {
                while (_pool.TryDequeue(out var entry))
                {
                    if (entry.Channel.IsOpen)
                    {
                        return new PooledChannelLease(entry.Channel, entry.Tracker, this);
                    }

                    await entry.Tracker.DisposeAsync();
                    await entry.Channel.DisposeAsync();
                }

                var conn = await _connection.GetConnectionAsync();
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                var channel = await conn.CreateChannelAsync(options, cancellationToken);
                var tracker = new ChannelConfirmTracker(channel);
                return new PooledChannelLease(channel, tracker, this);
            }
            catch
            {
                _gate.Release();
                throw;
            }
        }

        /// <summary>
        /// 归还通道到池
        /// </summary>
        public async ValueTask ReturnAsync(IChannel channel, ChannelConfirmTracker tracker)
        {
            // 池已释放（如应用关停时池先于租约归还被释放）：仅清理资源，不触碰已释放的信号量。
            if (_disposed)
            {
                await tracker.DisposeAsync();
                await channel.DisposeAsync();
                return;
            }

            if (!channel.IsOpen)
            {
                await tracker.DisposeAsync();
                await channel.DisposeAsync();
            }
            else
            {
                _pool.Enqueue((channel, tracker));
            }

            _gate.Release();
        }

        /// <summary>
        /// 释放通道池资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            while (_pool.TryDequeue(out var entry))
            {
                await entry.Tracker.DisposeAsync();
                await entry.Channel.DisposeAsync();
            }

            _gate.Dispose();
        }
    }
}
