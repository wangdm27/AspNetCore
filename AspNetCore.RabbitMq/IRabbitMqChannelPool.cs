using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqChannelPool : IAsyncDisposable
    {
        ValueTask<PooledChannelLease> RentAsync(CancellationToken cancellationToken = default);
    }

    public readonly struct PooledChannelLease : IAsyncDisposable
    {
        private readonly IRabbitMqChannelPoolLease _lease;

        internal PooledChannelLease(IChannel channel, IRabbitMqChannelPoolLease lease)
        {
            Channel = channel;
            _lease = lease;
        }

        public IChannel Channel { get; }

        public ValueTask DisposeAsync() => _lease.ReturnAsync(Channel);
    }

    internal interface IRabbitMqChannelPoolLease
    {
        ValueTask ReturnAsync(IChannel channel);
    }
}
