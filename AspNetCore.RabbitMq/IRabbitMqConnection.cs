using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqConnection : IAsyncDisposable
    {
        Task<IConnection> GetConnectionAsync();
    }
}
