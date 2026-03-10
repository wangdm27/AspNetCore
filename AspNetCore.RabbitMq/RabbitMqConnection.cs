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
            // 第一次检查：快速路径，避免获取锁的开销
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // 获取锁，确保线程安全
            await _connectionLock.WaitAsync();
            try
            {
                // 第二次检查：双重检查锁定模式，防止竞态条件
                if (_connection is { IsOpen: true })
                {
                    return _connection;
                }

                // 释放旧连接
                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                }

                // 创建新连接
                _connection = await _factory.CreateConnectionAsync();
                return _connection;
            }
            finally
            {
                // 确保锁被释放，即使发生异常
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
