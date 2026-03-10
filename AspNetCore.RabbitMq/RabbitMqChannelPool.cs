using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ通道池实现
    /// </summary>
    /// <remarks>
    /// 管理RabbitMQ通道的创建、复用和释放，提高性能并减少资源消耗
    /// </remarks>
    internal sealed class RabbitMqChannelPool : IRabbitMqChannelPool, IRabbitMqChannelPoolLease
    {
        /// <summary>
        /// RabbitMQ连接实例
        /// </summary>
        private readonly IRabbitMqConnection _connection;
        
        /// <summary>
        /// 通道池，使用并发队列存储空闲通道
        /// </summary>
        private readonly ConcurrentQueue<IChannel> _pool = new();
        
        /// <summary>
        /// 信号量，用于控制最大并发通道数
        /// </summary>
        private readonly SemaphoreSlim _gate;
        
        /// <summary>
        /// 释放状态标记
        /// </summary>
        private volatile bool _disposed;

        /// <summary>
        /// 初始化通道池
        /// </summary>
        /// <param name="connection">RabbitMQ连接实例</param>
        /// <param name="options">RabbitMQ配置选项</param>
        public RabbitMqChannelPool(IRabbitMqConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _gate = new SemaphoreSlim(options.ChannelPoolSize, options.ChannelPoolSize);
        }

        /// <summary>
        /// 从通道池获取一个通道
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>池化通道租赁对象</returns>
        /// <remarks>
        /// 优先从池中获取空闲通道，如果没有则创建新通道
        /// </remarks>
        public async ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // 等待信号量，控制并发通道数
            await _gate.WaitAsync(cancellationToken);

            try
            {
                // 尝试从池中获取通道
                while (_pool.TryDequeue(out var channel))
                {
                    if (channel.IsOpen)
                    {
                        return new PooledChannelLease(channel, this);
                    }

                    // 通道已关闭，释放资源
                    await channel.DisposeAsync();
                }

                // 池中没有可用通道，创建新通道
                var conn = await _connection.GetConnectionAsync();
                var created = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
                return new PooledChannelLease(created, this);
            }
            catch
            {
                // 发生异常时释放信号量
                _gate.Release();
                throw;
            }
        }

        /// <summary>
        /// 归还通道到池
        /// </summary>
        /// <param name="channel">要归还的通道</param>
        /// <remarks>
        /// 如果池已释放或通道已关闭，则直接释放通道
        /// 否则将通道放回池中供后续使用
        /// </remarks>
        public async ValueTask ReturnAsync(IChannel channel)
        {
            if (_disposed || !channel.IsOpen)
            {
                // 池已释放或通道已关闭，直接释放通道
                await channel.DisposeAsync();
            }
            else
            {
                // 将通道放回池中
                _pool.Enqueue(channel);
            }

            // 释放信号量
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
            
            // 释放池中所有通道
            while (_pool.TryDequeue(out var channel))
            {
                await channel.DisposeAsync();
            }

            // 释放信号量
            _gate.Dispose();
        }
    }
}