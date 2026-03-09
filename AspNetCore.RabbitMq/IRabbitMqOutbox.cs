using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqOutbox
    {
        ValueTask EnqueueAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default);
    }
}
