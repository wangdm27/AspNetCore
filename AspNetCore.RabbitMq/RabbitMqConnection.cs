using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public sealed class RabbitMqConnection : IRabbitMqConnection
    {
        private readonly RabbitMqOptions _options;
        private IConnection? _connection;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public RabbitMqConnection(RabbitMqOptions options)
        {
            _options = options;
        }

        public async Task<IConnection> GetConnectionAsync()
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            await _lock.WaitAsync();
            try
            {
                if (_connection != null && _connection.IsOpen)
                    return _connection;


                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    ConsumerDispatchConcurrency = _options.ConsumerConcurrency
                };


                _connection = await factory.CreateConnectionAsync();
                return _connection;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }
}
