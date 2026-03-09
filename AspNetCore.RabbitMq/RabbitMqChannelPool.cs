using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqChannelPool : IRabbitMqChannelPool, IRabbitMqChannelPoolLease
    {
        private readonly IRabbitMqConnection _connection;
        private readonly ConcurrentQueue<IChannel> _pool = new();
        private readonly SemaphoreSlim _gate;
        private volatile bool _disposed;

        public RabbitMqChannelPool(IRabbitMqConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _gate = new SemaphoreSlim(options.ChannelPoolSize, options.ChannelPoolSize);
        }

        public async ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _gate.WaitAsync(cancellationToken);

            try
            {
                while (_pool.TryDequeue(out var channel))
                {
                    if (channel.IsOpen)
                    {
                        return new PooledChannelLease(channel, this);
                    }

                    await channel.DisposeAsync();
                }

                var conn = await _connection.GetConnectionAsync();
                var created = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
                return new PooledChannelLease(created, this);
            }
            catch
            {
                _gate.Release();
                throw;
            }
        }

        public async ValueTask ReturnAsync(IChannel channel)
        {
            if (_disposed || !channel.IsOpen)
            {
                await channel.DisposeAsync();
            }
            else
            {
                _pool.Enqueue(channel);
            }

            _gate.Release();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            while (_pool.TryDequeue(out var channel))
            {
                await channel.DisposeAsync();
            }

            _gate.Dispose();
        }
    }
}
