using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqConnection : IRabbitMqConnection
    {
        private readonly ConnectionFactory _factory;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private IConnection? _connection;

        public RabbitMqConnection(RabbitMqOptions options)
        {
            _factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,
                AutomaticRecoveryEnabled = options.AutomaticRecoveryEnabled,
                TopologyRecoveryEnabled = options.TopologyRecoveryEnabled,
                NetworkRecoveryInterval = options.NetworkRecoveryInterval
            };
        }

        public async Task<IConnection> GetConnectionAsync()
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            await _connectionLock.WaitAsync();
            try
            {
                if (_connection is { IsOpen: true })
                {
                    return _connection;
                }

                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                }

                _connection = await _factory.CreateConnectionAsync();
                return _connection;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connectionLock.Dispose();
        }
    }
}
