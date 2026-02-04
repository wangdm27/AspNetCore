using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqConnection : IRabbitMqConnection
    {
        private readonly Task<IConnection> _connectionTask;

        public RabbitMqConnection(RabbitMqOptions options)
        {
            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            };


            // RabbitMQ.Client 7.x 起改为 CreateConnectionAsync
            _connectionTask = factory.CreateConnectionAsync();
        }


        public Task<IConnection> GetConnectionAsync() => _connectionTask;


        public async ValueTask DisposeAsync()
        {
            var conn = await _connectionTask;
            conn.Dispose();
        }
    }
}
