using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqPublisher
    {
        Task PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            bool confirm = true,
            int? delayMs = null,
            CancellationToken cancellationToken = default);
    }
}
